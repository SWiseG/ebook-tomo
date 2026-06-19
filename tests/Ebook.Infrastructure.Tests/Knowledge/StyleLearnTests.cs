using System.Text.Json;
using Ebook.Application;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Application.Knowledge;
using Ebook.Application.Publishing;
using Ebook.Application.Social;
using Ebook.Application.Video;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
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

namespace Ebook.Infrastructure.Tests.Knowledge;

/// <summary>
/// E15 — loop de aprendizado de estilo: o StyleLearnJobHandler analisa a capa (via IStyleAnalyzer fake)
/// e grava um KnowledgeAsset(MediaStyle) por nicho. Cobre o caminho feliz, a degradação suave quando a
/// visão falha (sem dead-letter) e a guarda de frescor (não reanalisa um playbook recente).
/// </summary>
public class StyleLearnTests
{
    private static ServiceProvider Build(Action<IServiceCollection>? extra = null) =>
        TestHost.Build(s =>
        {
            s.AddApplication();
            s.AddSingleton<IAiGateway>(new FakeAiGateway());
            s.AddSingleton<IPdfRenderer>(new FakePdfRenderer());
            s.AddSingleton<IImageComposer>(new FakeImageComposer());
            s.AddSingleton<IPhotoProvider, NullPhotoProvider>();
            s.AddSingleton<IKiwifyPublisher>(new FakeKiwifyPublisher());
            s.AddSingleton<ISocialPublisher>(new FakeSocialPublisher());
            s.AddSingleton<ITtsEngine>(new FakeTtsEngine());
            s.AddSingleton<IVideoComposer>(new FakeVideoComposer());
            s.AddSingleton<IArtifactStore, FileArtifactStore>();
            s.AddScoped<IJobQueue, JobQueue>();
            s.AddScoped<ISettingsStore, SettingsStore>();
            s.AddScoped<INicheRepository, NicheRepository>();
            s.AddScoped<IProductRepository, ProductRepository>();
            s.AddScoped<IArtifactRepository, ArtifactRepository>();
            s.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
            s.AddScoped<ITrendSnapshotRepository, TrendSnapshotRepository>();
            s.AddScoped<ISocialPostRepository, SocialPostRepository>();
            s.AddScoped<IProductReader, ProductReader>();
            extra?.Invoke(s);
        });

    /// <summary>Semeia nicho + produto e grava uma capa fake no artifact store. Devolve (nicheId, productId, slug).</summary>
    private static async Task<(Guid NicheId, Guid ProductId, string Slug)> SeedWithCoverAsync(
        ServiceProvider provider, bool withCover = true, string slug = "financas-pessoais")
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var now = DateTime.UtcNow;

        var niche = Niche.Discover(slug, "Finanças Pessoais", 0.9, "{}", 1, now);
        db.Niches.Add(niche);
        var product = Product.Create(niche.Id, slug, "Virada Financeira", QualityTier.Commercial, now);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        if (withCover)
        {
            var artifactStore = scope.ServiceProvider.GetRequiredService<IArtifactStore>();
            await artifactStore.WriteBytesAsync(ContentPaths.Cover(slug), [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4], CancellationToken.None);
        }

        return (niche.Id, product.Id, slug);
    }

    private static async Task DrainAsync(ServiceProvider provider)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var worker = new JobWorker(scopeFactory, NullLogger<JobWorker>.Instance);
        for (var i = 0; i < 30 && await worker.ProcessNextAsync(CancellationToken.None); i++)
        {
            // drena a fila
        }
    }

    private static async Task EnqueueLearnAsync(ServiceProvider provider, Guid nicheId, Guid productId)
    {
        using var scope = provider.CreateScope();
        var payload = JsonSerializer.Serialize(new StyleLearnJobPayload(nicheId, productId));
        await scope.ServiceProvider.GetRequiredService<IJobQueue>()
            .EnqueueAsync(new JobRequest(KnowledgeJobs.StyleLearn, payload, KnowledgeJobs.StyleLearnKey(nicheId, 1), productId));
    }

    [Fact]
    public async Task Analisa_capa_e_grava_playbook_MediaStyle()
    {
        using var provider = Build();
        var (nicheId, productId, slug) = await SeedWithCoverAsync(provider);

        await EnqueueLearnAsync(provider, nicheId, productId);
        await DrainAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();

        var asset = await db.KnowledgeAssets.AsNoTracking()
            .SingleOrDefaultAsync(a => a.NicheId == nicheId && a.Type == KnowledgeAssetType.MediaStyle);
        Assert.NotNull(asset);
        Assert.Contains("warm gold accents", asset!.KeywordsCsv, StringComparison.Ordinal);
        Assert.NotNull(await fileStore.ReadTextAsync(asset.Path));

        var job = await db.Jobs.AsNoTracking().SingleAsync(j => j.Type == KnowledgeJobs.StyleLearn);
        Assert.Equal(JobStatus.Succeeded, job.Status);
    }

    [Fact]
    public async Task Sem_capa_nao_grava_playbook_e_conclui()
    {
        using var provider = Build();
        var (nicheId, productId, _) = await SeedWithCoverAsync(provider, withCover: false);

        await EnqueueLearnAsync(provider, nicheId, productId);
        await DrainAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();

        Assert.False(await db.KnowledgeAssets.AnyAsync(a => a.Type == KnowledgeAssetType.MediaStyle));
        var job = await db.Jobs.AsNoTracking().SingleAsync(j => j.Type == KnowledgeJobs.StyleLearn);
        Assert.Equal(JobStatus.Succeeded, job.Status); // conclui sem dead-letter
    }

    [Fact]
    public async Task Falha_de_visao_e_suave_sem_dead_letter()
    {
        using var provider = Build(s => s.AddSingleton<IStyleAnalyzer>(new FakeStyleAnalyzer(fail: true)));
        var (nicheId, productId, _) = await SeedWithCoverAsync(provider);

        await EnqueueLearnAsync(provider, nicheId, productId);
        await DrainAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();

        Assert.False(await db.KnowledgeAssets.AnyAsync(a => a.Type == KnowledgeAssetType.MediaStyle));
        var job = await db.Jobs.AsNoTracking().SingleAsync(j => j.Type == KnowledgeJobs.StyleLearn);
        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Equal(1, job.Attempts); // não reagendou nem virou dead-letter
    }

    [Fact]
    public async Task Playbook_recente_nao_e_reanalisado()
    {
        using var provider = Build();
        var (nicheId, productId, _) = await SeedWithCoverAsync(provider);
        var analyzer = (FakeStyleAnalyzer)provider.GetRequiredService<IStyleAnalyzer>();

        await EnqueueLearnAsync(provider, nicheId, productId);
        await DrainAsync(provider);
        Assert.Equal(1, analyzer.Calls);

        // segunda rodada (mesma semana): a guarda de frescor pula a reanálise
        using (var scope = provider.CreateScope())
        {
            var payload = JsonSerializer.Serialize(new StyleLearnJobPayload(nicheId, productId));
            await scope.ServiceProvider.GetRequiredService<IJobQueue>()
                .EnqueueAsync(new JobRequest(KnowledgeJobs.StyleLearn, payload, KnowledgeJobs.StyleLearnKey(nicheId, 1) + ":2", productId));
        }
        await DrainAsync(provider);

        Assert.Equal(1, analyzer.Calls); // não reanalisou
        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(1, await db.KnowledgeAssets.CountAsync(a => a.Type == KnowledgeAssetType.MediaStyle));
    }
}
