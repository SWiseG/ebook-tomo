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
using Ebook.Application.Video;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Ebook.Domain.Social;
using Ebook.Infrastructure.Content;
using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Jobs;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Settings;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Tests.Content;

/// <summary>
/// Pipeline de conteúdo ponta a ponta (E04 + E03) com IA fake determinística:
/// GenerateProduct → outline → capítulos → review → manuscrito + copy + preço.
/// </summary>
public class EbookGenerationTests
{
    private static (ServiceProvider Provider, FakeAiGateway Ai, FakePdfRenderer Pdf, FakeImageComposer Img, FakeEbookExporter Epub) Build()
    {
        var ai = new FakeAiGateway();
        var pdf = new FakePdfRenderer();
        var img = new FakeImageComposer();
        var epub = new FakeEbookExporter();
        var provider = TestHost.Build(s =>
        {
            s.AddApplication(); // dispatcher + KnowledgeService + handlers (command/query/job) por scan
            s.AddSingleton<IAiGateway>(ai);
            s.AddSingleton<IPdfRenderer>(pdf);
            s.AddSingleton<IEbookExporter>(epub);
            s.AddSingleton<IImageComposer>(img);
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
        });
        return (provider, ai, pdf, img, epub);
    }

    private static async Task<Guid> SeedNicheAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var niche = Niche.Discover("financas-autonomos", "Finanças para Autônomos", 0.8, "{}", 1, DateTime.UtcNow);
        db.Niches.Add(niche);
        await db.SaveChangesAsync();
        return niche.Id;
    }

    private static async Task<Guid> GenerateAsync(ServiceProvider provider, Guid nicheId, QualityTier tier)
    {
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var result = await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, tier));
        Assert.True(result.IsSuccess);
        return result.Value.ProductId;
    }

    private static async Task RunJobsAsync(ServiceProvider provider)
    {
        var worker = new JobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<JobWorker>>());

        for (var i = 0; i < 100 && await worker.ProcessNextAsync(CancellationToken.None); i++)
        {
            // drena a fila: cada job pode enfileirar os próximos
        }
    }

    [Fact]
    public async Task Pipeline_gera_manuscrito_copy_capa_pdf_epub_e_avanca_ate_lp()
    {
        var (provider, ai, pdf, img, epub) = Build();
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);
        var productId = await GenerateAsync(provider, nicheId, QualityTier.Commercial);

        await RunJobsAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var artifactStore = scope.ServiceProvider.GetRequiredService<IArtifactStore>();

        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(ProductStatus.AwaitingApproval, product.Status); // LP pronta → gate de aprovação (padrão)
        Assert.Equal(ProductStage.Lp, product.Stage); // fim do pipeline de conteúdo
        Assert.Equal("Dinheiro Sob Controle", product.Title); // título refinado pelo outline
        Assert.Equal(27m, product.Price);
        Assert.Contains("headline", product.SalesCopyJson, StringComparison.Ordinal);

        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1));
        Assert.NotNull(manuscript);
        Assert.Contains("# Dinheiro Sob Controle", manuscript, StringComparison.Ordinal);
        Assert.Contains("## Capítulo 1 — Mapeie seu dinheiro", manuscript, StringComparison.Ordinal);
        Assert.Contains("## Capítulo 2 — Crie sua reserva", manuscript, StringComparison.Ordinal);
        Assert.Contains("Introdução envolvente", manuscript, StringComparison.Ordinal);
        Assert.Contains("CTA", manuscript, StringComparison.Ordinal);

        // capa (E09): renderizada uma vez + mockup, persistidas e embutidas no PDF
        Assert.Equal(1, img.CoverCount);
        Assert.Equal(1, img.MockupCount);
        Assert.NotNull(await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug)));
        Assert.NotNull(await artifactStore.ReadBytesAsync(ContentPaths.Mockup(product.Slug)));
        Assert.True(pdf.LastHadCover); // a capa foi embutida no PDF

        // PDF: renderizado uma vez, persistido no artifact store e indexado como Artifact
        Assert.Equal(1, pdf.RenderCount);
        Assert.Equal("Dinheiro Sob Controle", pdf.Last!.Title);
        var pdfBytes = await artifactStore.ReadBytesAsync(ContentPaths.Pdf(product.Slug, 1));
        Assert.NotNull(pdfBytes);
        Assert.StartsWith("%PDF", System.Text.Encoding.UTF8.GetString(pdfBytes![..4]), StringComparison.Ordinal);

        // landing page (E06): bundle HTML auto-contido com headline, capa embutida e CTA → redirect de checkout
        var lpBytes = await artifactStore.ReadBytesAsync(ContentPaths.LpBundle(product.Slug));
        Assert.NotNull(lpBytes);
        var lpHtml = System.Text.Encoding.UTF8.GetString(lpBytes!);
        Assert.Contains("Assuma o controle do seu dinheiro", lpHtml, StringComparison.Ordinal); // headline da copy
        Assert.Contains("data:image/png;base64,", lpHtml, StringComparison.Ordinal); // capa embutida
        Assert.Contains($"/go/{product.Slug}", lpHtml, StringComparison.Ordinal); // CTA aponta para o redirect
        Assert.Equal($"/lp/{product.Slug}", product.LpUrl); // URL pública (base relativa por padrão)

        // economia de tokens: pack gerado uma vez e reusado entre outline e sales-copy
        Assert.Equal(1, ai.CallsFor("knowledge.pack"));
        Assert.Equal(1, ai.CallsFor("ebook.outline"));
        Assert.Equal(2, ai.CallsFor("ebook.chapter"));
        Assert.Equal(1, ai.CallsFor("ebook.review"));
        Assert.Equal(1, ai.CallsFor("ebook.continuity")); // 1 chamada extra por e-book (A1)
        Assert.Equal(1, ai.CallsFor("ebook.sales-copy"));

        Assert.Equal(1, await db.KnowledgeAssets.CountAsync());
        Assert.Equal(1, await db.Artifacts.CountAsync(a => a.Type == ArtifactType.Manuscript));
        Assert.Equal(1, await db.Artifacts.CountAsync(a => a.Type == ArtifactType.Cover));
        Assert.Equal(1, await db.Artifacts.CountAsync(a => a.Type == ArtifactType.Mockup));
        Assert.Equal(1, await db.Artifacts.CountAsync(a => a.Type == ArtifactType.Pdf));
        Assert.Equal(1, await db.Artifacts.CountAsync(a => a.Type == ArtifactType.Epub));
        Assert.Equal(1, await db.Artifacts.CountAsync(a => a.Type == ArtifactType.LpBundle));
        Assert.Equal(8, await db.Jobs.CountAsync()); // outline + 2 capítulos + review + cover + pdf + epub + lp
        Assert.True(await db.Jobs.AllAsync(j => j.Status == JobStatus.Succeeded));

        // EPUB gerado uma vez pelo exporter fake
        Assert.Equal(1, epub.ExportCount);
        Assert.Equal("Dinheiro Sob Controle", epub.Last!.Title);
    }

    [Fact]
    public async Task Draft_nao_chama_revisao_de_IA()
    {
        var (provider, ai, _, _, _) = Build();
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);
        await GenerateAsync(provider, nicheId, QualityTier.Draft);

        await RunJobsAsync(provider);

        Assert.Equal(0, ai.CallsFor("ebook.review"));      // Draft usa moldura templada
        Assert.Equal(0, ai.CallsFor("ebook.continuity"));  // Draft pula continuidade
    }

    [Fact]
    public async Task Modo_Auto_publica_sem_gate_de_aprovacao()
    {
        var (provider, _, _, _, _) = Build();
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
            await settings.SetAsync(SettingKeys.PublishingRequiresApproval, false);
        }

        var productId = await GenerateAsync(provider, nicheId, QualityTier.Commercial);
        await RunJobsAsync(provider);

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(ProductStatus.Publishing, product.Status); // Auto: avança direto à publicação
        Assert.Equal(ProductStage.Publishing, product.Stage);
        Assert.NotNull(product.LpUrl);
    }

    [Fact]
    public async Task Knowledge_pack_e_reaproveitado_entre_produtos_do_mesmo_nicho()
    {
        var (provider, ai, _, _, _) = Build();
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);

        await GenerateAsync(provider, nicheId, QualityTier.Commercial);
        await RunJobsAsync(provider);
        await GenerateAsync(provider, nicheId, QualityTier.Commercial);
        await RunJobsAsync(provider);

        Assert.Equal(1, ai.CallsFor("knowledge.pack")); // gerado no 1º produto, reusado no 2º

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(2, await db.Products.CountAsync());
        Assert.Equal(1, await db.KnowledgeAssets.CountAsync());
    }

    [Fact]
    public async Task Reexecutar_jobs_e_idempotente_nao_duplica_artefatos()
    {
        var (provider, ai, pdf, img, epub) = Build();
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);
        var productId = await GenerateAsync(provider, nicheId, QualityTier.Commercial);

        await RunJobsAsync(provider);

        // força reexecução de todos os jobs (entrega at-least-once)
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
            await db.Jobs.ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Pending)
                .SetProperty(j => j.Attempts, 0)
                .SetProperty(j => j.ScheduledAtUtc, DateTime.UtcNow.AddSeconds(-1)));
        }

        var chapterCallsBefore = ai.CallsFor("ebook.chapter");
        var rendersBefore = pdf.RenderCount;
        var coversBefore = img.CoverCount;
        var epubsBefore = epub.ExportCount;
        await RunJobsAsync(provider);

        using var verify = provider.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await verifyDb.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

        Assert.Equal(ProductStage.Lp, product.Stage); // não avançou além do esperado
        Assert.Equal(ProductStatus.AwaitingApproval, product.Status); // transição não repetida
        Assert.Equal(chapterCallsBefore, ai.CallsFor("ebook.chapter")); // arquivos já existem: zero IA extra
        Assert.Equal(rendersBefore, pdf.RenderCount); // PDF já existe: zero re-render
        Assert.Equal(coversBefore, img.CoverCount); // capa já existe: zero re-render
        Assert.Equal(epubsBefore, epub.ExportCount); // EPUB já existe: zero re-export
        Assert.Equal(1, await verifyDb.Artifacts.CountAsync(a => a.Type == ArtifactType.Manuscript));
        Assert.Equal(1, await verifyDb.Artifacts.CountAsync(a => a.Type == ArtifactType.Cover));
        Assert.Equal(1, await verifyDb.Artifacts.CountAsync(a => a.Type == ArtifactType.Pdf));
        Assert.Equal(1, await verifyDb.Artifacts.CountAsync(a => a.Type == ArtifactType.Epub));
        Assert.Equal(1, await verifyDb.Artifacts.CountAsync(a => a.Type == ArtifactType.LpBundle));
    }
}
