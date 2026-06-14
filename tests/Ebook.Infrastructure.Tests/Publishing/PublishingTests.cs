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

namespace Ebook.Infrastructure.Tests.Publishing;

/// <summary>
/// E07 — gate de aprovação + orquestração de publicação (Kiwify) + webhooks de venda.
/// Publisher fake (sem Playwright/rede); o fluxo é dirigido por Outbox + JobWorker.
/// </summary>
public class PublishingTests
{
    private static (ServiceProvider Provider, FakeKiwifyPublisher Kiwify) Build()
    {
        var kiwify = new FakeKiwifyPublisher();
        var provider = TestHost.Build(s =>
        {
            s.AddApplication();
            s.AddSingleton<IAiGateway>(new FakeAiGateway());
            s.AddSingleton<IPdfRenderer>(new FakePdfRenderer());
            s.AddSingleton<IImageComposer>(new FakeImageComposer());
            s.AddSingleton<IPhotoProvider, NullPhotoProvider>();
            s.AddSingleton<IKiwifyPublisher>(kiwify);
            s.AddSingleton<ISocialPublisher>(new FakeSocialPublisher());
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
        return (provider, kiwify);
    }

    private static async Task<Guid> SeedAwaitingApprovalAsync(ServiceProvider provider, string slug = "guia-x")
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = Product.Create(Guid.NewGuid(), slug, "Guia X", QualityTier.Commercial, DateTime.UtcNow);
        product.SetPricing(27m, "BRL");
        product.SetSalesCopy("{\"headline\":\"Assuma o controle\"}");
        product.AdvanceStage(); // Writing
        product.AdvanceStage(); // Review
        product.AdvanceStage(); // Pdf
        product.AdvanceStage(); // Lp
        product.SetLpUrl($"/lp/{slug}");
        product.SubmitForApproval(); // → AwaitingApproval
        db.Products.Add(product);
        await db.SaveChangesAsync(); // save simples: eventos do seed não vão ao outbox
        return product.Id;
    }

    private static async Task SetAsync(ServiceProvider provider, string key, bool value)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SetAsync(key, value);
    }

    private static async Task<T> SendAsync<T>(ServiceProvider provider, ICommand<T> command)
    {
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }

    private static async Task DrainAsync(ServiceProvider provider)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var outbox = new OutboxDispatcher(scopeFactory, NullLogger<OutboxDispatcher>.Instance);
        var worker = new JobWorker(scopeFactory, NullLogger<JobWorker>.Instance);
        for (var i = 0; i < 50; i++)
        {
            var events = await outbox.ProcessPendingOnceAsync(CancellationToken.None);
            var job = await worker.ProcessNextAsync(CancellationToken.None);
            if (events == 0 && !job)
            {
                return;
            }
        }
    }

    private static async Task<Product> LoadAsync(ServiceProvider provider, Guid id)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        return await db.Products.AsNoTracking().SingleAsync(p => p.Id == id);
    }

    [Fact]
    public async Task Modo_auto_aprovar_publica_na_kiwify_e_vai_live()
    {
        var (provider, kiwify) = Build();
        using var _ = provider;
        var id = await SeedAwaitingApprovalAsync(provider);
        await SetAsync(provider, SettingKeys.KiwifyAutoPublish, true);

        await SendAsync(provider, new ApproveProductCommand(id)); // → Publishing + ProductPublishingStarted
        await DrainAsync(provider); // outbox enfileira kiwify.publish → worker publica → Live

        var product = await LoadAsync(provider, id);
        Assert.Equal(ProductStatus.Live, product.Status);
        Assert.Equal(ProductStage.Live, product.Stage);
        Assert.Equal("kw-guia-x", product.KiwifyProductId);
        Assert.Equal("https://pay.kiwify.com.br/guia-x", product.CheckoutUrl);
        Assert.Equal(1, kiwify.Count);
        Assert.Equal("Assuma o controle", kiwify.Last!.Description); // headline da copy
    }

    [Fact]
    public async Task Modo_manual_aprovar_nao_enfileira_job_e_conclui_pelo_painel()
    {
        var (provider, kiwify) = Build();
        using var _ = provider;
        var id = await SeedAwaitingApprovalAsync(provider); // autoPublish padrão = false

        await SendAsync(provider, new ApproveProductCommand(id));
        await DrainAsync(provider);

        var pending = await LoadAsync(provider, id);
        Assert.Equal(ProductStatus.Publishing, pending.Status); // aguardando conclusão manual
        Assert.Equal(0, kiwify.Count); // nenhuma automação disparada

        using (var scope = provider.CreateScope())
        {
            var jobs = await scope.ServiceProvider.GetRequiredService<EbookDbContext>().Jobs.CountAsync();
            Assert.Equal(0, jobs); // nenhum job de publicação enfileirado
        }

        await SendAsync(provider, new CompletePublishingCommand(id, "kw-manual", "https://pay.kiwify.com.br/manual"));

        var live = await LoadAsync(provider, id);
        Assert.Equal(ProductStatus.Live, live.Status);
        Assert.Equal("kw-manual", live.KiwifyProductId);
        Assert.Equal("https://pay.kiwify.com.br/manual", live.CheckoutUrl);
    }

    [Fact]
    public async Task Rejeitar_devolve_para_retrabalho()
    {
        var (provider, _) = Build();
        using var _p = provider;
        var id = await SeedAwaitingApprovalAsync(provider);

        await SendAsync(provider, new RejectProductCommand(id, "Capítulo 2 fraco"));

        var product = await LoadAsync(provider, id);
        Assert.Equal(ProductStatus.Reworking, product.Status);
        Assert.Equal(ProductStage.Writing, product.Stage);
    }

    [Fact]
    public async Task Webhook_registra_sale_event_idempotente_e_resolve_produto()
    {
        var (provider, _) = Build();
        using var _p = provider;
        var id = await SeedAwaitingApprovalAsync(provider);
        await SetAsync(provider, SettingKeys.KiwifyAutoPublish, true);
        await SendAsync(provider, new ApproveProductCommand(id));
        await DrainAsync(provider); // produto Live com KiwifyProductId = "kw-guia-x"

        var sale = new RecordSaleCommand(
            "ORDER-1", SaleType.Sale, 27m, 24m, "BRL", "kw-guia-x",
            "instagram", "lancamento", DateTime.UtcNow, "{\"order_id\":\"ORDER-1\"}");

        await SendAsync(provider, sale);
        await SendAsync(provider, sale); // reentrega do webhook

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var events = await db.SaleEvents.AsNoTracking().ToListAsync();
        Assert.Single(events); // idempotente por KiwifyOrderId
        Assert.Equal(id, events[0].ProductId); // resolvido pelo id Kiwify
        Assert.Equal(SaleType.Sale, events[0].Type);
        Assert.Equal(27m, events[0].GrossAmount);

        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        Assert.NotNull(await fileStore.ReadTextAsync(SalePaths.Raw("ORDER-1"))); // payload bruto guardado
    }
}
