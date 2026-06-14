using Ebook.Application;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Application.Publishing;
using Ebook.Application.Social;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Ebook.Domain.Sales;
using Ebook.Domain.Social;
using Ebook.Infrastructure.Content;
using Ebook.Infrastructure.Events;
using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Jobs;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Settings;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Infrastructure.Tests.Social;

/// <summary>
/// E08 — calendário de conteúdo (IA + cards), agendamento e publicação social.
/// IA e publishers fakes; o fluxo é dirigido por Outbox + JobWorker.
/// </summary>
public class SocialTests
{
    private static (ServiceProvider Provider, FakeAiGateway Ai, FakeImageComposer Img, FakeSocialPublisher Social) Build()
    {
        var ai = new FakeAiGateway();
        var img = new FakeImageComposer();
        var social = new FakeSocialPublisher();
        var provider = TestHost.Build(s =>
        {
            s.AddApplication();
            s.AddSingleton<IAiGateway>(ai);
            s.AddSingleton<IPdfRenderer>(new FakePdfRenderer());
            s.AddSingleton<IImageComposer>(img);
            s.AddSingleton<IPhotoProvider, NullPhotoProvider>();
            s.AddSingleton<IKiwifyPublisher>(new FakeKiwifyPublisher());
            s.AddSingleton<ISocialPublisher>(social);
            s.AddSingleton<IArtifactStore, FileArtifactStore>();
            s.AddScoped<IJobQueue, JobQueue>();
            s.AddScoped<ISettingsStore, SettingsStore>();
            s.AddScoped<INicheRepository, NicheRepository>();
            s.AddScoped<IProductRepository, ProductRepository>();
            s.AddScoped<IArtifactRepository, ArtifactRepository>();
            s.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
            s.AddScoped<ITrendSnapshotRepository, TrendSnapshotRepository>();
            s.AddScoped<ISaleRepository, SaleRepository>();
            s.AddScoped<ISocialPostRepository, SocialPostRepository>();
            s.AddScoped<IProductReader, ProductReader>();
        });
        return (provider, ai, img, social);
    }

    /// <summary>Semeia um produto já Live (pronto para gerar calendário), com save simples.</summary>
    private static async Task<Guid> SeedLiveProductAsync(ServiceProvider provider, string slug = "guia-social")
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var now = DateTime.UtcNow;
        var product = Product.Create(Guid.NewGuid(), slug, "Guia Social", QualityTier.Commercial, now);
        product.SetSalesCopy("{\"headline\":\"Assuma o controle\"}");
        product.AdvanceStage();
        product.AdvanceStage();
        product.AdvanceStage();
        product.AdvanceStage(); // → Lp
        product.SubmitForApproval();
        product.Approve(); // → Publishing
        product.MarkPublished("kw-x", "https://pay.kiwify.com.br/x", $"/lp/{slug}", now); // → Live
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task EnqueueAsync(ServiceProvider provider, JobRequest request)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync(request);
    }

    private static async Task SetAsync(ServiceProvider provider, string key, bool value)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SetAsync(key, value);
    }

    private static async Task DrainAsync(ServiceProvider provider)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var outbox = new OutboxDispatcher(scopeFactory, NullLogger<OutboxDispatcher>.Instance);
        var worker = new JobWorker(scopeFactory, NullLogger<JobWorker>.Instance);
        for (var i = 0; i < 60; i++)
        {
            var events = await outbox.ProcessPendingOnceAsync(CancellationToken.None);
            var job = await worker.ProcessNextAsync(CancellationToken.None);
            if (events == 0 && !job)
            {
                return;
            }
        }
    }

    private static string CalendarPayload(Guid productId) =>
        System.Text.Json.JsonSerializer.Serialize(new CalendarJobPayload(productId));

    [Fact]
    public async Task Gera_calendario_com_posts_e_cards_idempotente()
    {
        var (provider, ai, img, _) = Build();
        using var _p = provider;
        var id = await SeedLiveProductAsync(provider);

        await EnqueueAsync(provider, new JobRequest(SocialJobs.Calendar, CalendarPayload(id), SocialJobs.CalendarKey(id), id));
        await DrainAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var artifactStore = scope.ServiceProvider.GetRequiredService<IArtifactStore>();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();

        var posts = await db.SocialPosts.AsNoTracking().OrderBy(p => p.Day).ToListAsync();
        Assert.Equal(2, posts.Count);
        Assert.All(posts, p => Assert.Equal(SocialPostStatus.Planned, p.Status));
        Assert.Equal(SocialNetwork.Instagram, posts[0].Network);
        Assert.Equal(SocialPostType.Launch, posts[0].PostType);
        Assert.Contains("utm_source=instagram", posts[0].Utm, StringComparison.Ordinal);
        Assert.Equal(2, img.SocialCount); // um card por post (E09)
        Assert.NotNull(await artifactStore.ReadBytesAsync(ContentPaths.SocialCard("guia-social", 1)));
        Assert.NotNull(await fileStore.ReadTextAsync(ContentPaths.SocialCalendar("guia-social")));
        Assert.Equal(1, ai.CallsFor("social.calendar"));

        // reexecuta a geração → idempotente (sem novos posts nem chamadas de IA)
        await EnqueueAsync(provider, new JobRequest(SocialJobs.Calendar, CalendarPayload(id), SocialJobs.CalendarKey(id) + ":2", id));
        await DrainAsync(provider);
        Assert.Equal(2, await db.SocialPosts.AsNoTracking().CountAsync());
        Assert.Equal(1, ai.CallsFor("social.calendar"));
    }

    [Fact]
    public async Task Produto_publicado_dispara_geracao_de_calendario()
    {
        var (provider, ai, _, _) = Build();
        using var _p = provider;

        // produto em AwaitingApproval; auto-publish Kiwify → Live → ProductPublished → social.calendar
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
            var now = DateTime.UtcNow;
            var product = Product.Create(Guid.NewGuid(), "guia-evt", "Guia Evt", QualityTier.Commercial, now);
            product.SetSalesCopy("{\"headline\":\"x\"}");
            product.AdvanceStage();
            product.AdvanceStage();
            product.AdvanceStage();
            product.AdvanceStage();
            product.SubmitForApproval();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        await SetAsync(provider, SettingKeys.KiwifyAutoPublish, true);
        Guid id;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
            id = await db.Products.AsNoTracking().Select(p => p.Id).SingleAsync();
        }

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(new ApproveProductCommand(id));
        }
        await DrainAsync(provider);

        using var verify = provider.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(ProductStatus.Live, (await vdb.Products.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(2, await vdb.SocialPosts.AsNoTracking().CountAsync()); // calendário gerado pelo evento
        Assert.Equal(1, ai.CallsFor("social.calendar"));
    }

    [Fact]
    public async Task Dispatch_publica_posts_vencidos_quando_auto_ligado()
    {
        var (provider, _, _, social) = Build();
        using var _p = provider;
        var id = await SeedLiveProductAsync(provider);

        await EnqueueAsync(provider, new JobRequest(SocialJobs.Calendar, CalendarPayload(id), SocialJobs.CalendarKey(id), id));
        await DrainAsync(provider);
        await ForceDuePostsAsync(provider);
        await SetAsync(provider, SettingKeys.SocialAutoPublish, true);

        await EnqueueAsync(provider, new JobRequest(SocialJobs.Dispatch, "{}", SocialJobs.DispatchKey(DateTime.UtcNow)));
        await DrainAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var posts = await db.SocialPosts.AsNoTracking().ToListAsync();
        Assert.All(posts, p => Assert.Equal(SocialPostStatus.Published, p.Status));
        Assert.All(posts, p => Assert.NotNull(p.ExternalId));
        Assert.Equal(2, social.Count);
    }

    [Fact]
    public async Task Dispatch_nao_publica_quando_auto_desligado()
    {
        var (provider, _, _, social) = Build();
        using var _p = provider;
        var id = await SeedLiveProductAsync(provider);

        await EnqueueAsync(provider, new JobRequest(SocialJobs.Calendar, CalendarPayload(id), SocialJobs.CalendarKey(id), id));
        await DrainAsync(provider);
        await ForceDuePostsAsync(provider);

        await EnqueueAsync(provider, new JobRequest(SocialJobs.Dispatch, "{}", SocialJobs.DispatchKey(DateTime.UtcNow)));
        await DrainAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.All(await db.SocialPosts.AsNoTracking().ToListAsync(),
            p => Assert.Equal(SocialPostStatus.Planned, p.Status)); // só agendados
        Assert.Equal(0, social.Count); // nada publicado
    }

    private static async Task ForceDuePostsAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        await db.SocialPosts.ExecuteUpdateAsync(s =>
            s.SetProperty(p => p.ScheduledAtUtc, DateTime.UtcNow.AddMinutes(-5)));
    }
}
