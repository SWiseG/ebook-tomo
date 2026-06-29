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
using Ebook.Infrastructure.Events;
using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Jobs;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Settings;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Infrastructure.Tests.Content;

/// <summary>
/// Testes do gate de auditoria de conversão (B2).
/// FakeAiGateway retorna score 72 para ebook.audit.
/// Limiar 70 → aprovado; limiar 75 → reprovado (retry).
/// </summary>
public class AuditGateTests
{
    private static (ServiceProvider Provider, FakeAiGateway Ai) Build()
    {
        var ai = new FakeAiGateway();
        var provider = TestHost.Build(s =>
        {
            s.AddApplication();
            s.AddSingleton<IAiGateway>(ai);
            s.AddSingleton<IPdfRenderer>(new FakePdfRenderer());
            s.AddSingleton<IEbookExporter>(new FakeEbookExporter());
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
            s.AddScoped<ILpVariantRepository, LpVariantRepository>();
            s.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
            s.AddScoped<ITrendSnapshotRepository, TrendSnapshotRepository>();
            s.AddScoped<ISocialPostRepository, SocialPostRepository>();
            s.AddScoped<IProductReader, ProductReader>();
        });
        return (provider, ai);
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

    private static async Task<Guid> GenerateAsync(ServiceProvider provider, Guid nicheId)
    {
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var result = await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Commercial));
        Assert.True(result.IsSuccess);
        return result.Value.ProductId;
    }

    private static async Task RunJobsAsync(ServiceProvider provider, int maxJobs = 100)
    {
        var worker = new JobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<JobWorker>>());

        for (var i = 0; i < maxJobs && await worker.ProcessNextAsync(CancellationToken.None); i++) { }
    }

    private static OutboxDispatcher BuildDispatcher(ServiceProvider provider) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxDispatcher>.Instance);

    private static async Task ConfigureSettingsAsync(
        ServiceProvider provider, int minScore, int maxRetries = 1)
    {
        using var scope = provider.CreateScope();
        var s = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        await s.SetAsync(SettingKeys.AuditGateMinScore, minScore);
        await s.SetAsync(SettingKeys.AuditMaxRetries, maxRetries);
    }

    // ── Cenário 1: gate desligado (minScore=0) — comportamento atual, não-regressão ─────────────

    [Fact]
    public async Task Gate_desligado_comporta_igual_ao_pipeline_atual()
    {
        var (provider, ai) = Build();
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);

        // minScore=0 (default): gate desligado, nenhuma auditoria chamada
        var productId = await GenerateAsync(provider, nicheId);
        await RunJobsAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

        Assert.Equal(ProductStage.Lp, product.Stage);
        Assert.Equal(0, ai.CallsFor("ebook.audit")); // gate desligado: sem chamada de auditoria
    }

    // ── Cenário 2: score >= limiar → Cover enfileirado normalmente ───────────────────────────────

    [Fact]
    public async Task Score_acima_do_limiar_avanca_para_cover()
    {
        // FakeAi retorna score 72; limiar 70 → 72 >= 70 → aprovado
        var (provider, ai) = Build();
        using var _ = provider;
        await ConfigureSettingsAsync(provider, minScore: 70, maxRetries: 1);

        var nicheId = await SeedNicheAsync(provider);
        var productId = await GenerateAsync(provider, nicheId);
        await RunJobsAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

        Assert.Equal(ProductStage.Lp, product.Stage); // pipeline completo
        Assert.Equal(1, ai.CallsFor("ebook.audit"));  // auditoria chamada uma vez

        // arquivo de estado persistido com o score
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var stateJson = await fileStore.ReadTextAsync(ContentPaths.AuditState(product.Slug));
        Assert.NotNull(stateJson);
        Assert.Contains("72", stateJson, StringComparison.Ordinal);

        // Cover job existe (gate aprovou)
        Assert.True(await db.Jobs.AnyAsync(j => j.Type == ContentJobs.Cover));
    }

    // ── Cenário 3: score < limiar, tentativas disponíveis → ManuscriptAuditFailed + retry ───────

    [Fact]
    public async Task Score_abaixo_do_limiar_emite_evento_e_nao_enfileira_cover_na_primeira_rodada()
    {
        // FakeAi retorna score 72; limiar 75 → 72 < 75 → falha → retry
        var (provider, ai) = Build();
        using var _ = provider;
        await ConfigureSettingsAsync(provider, minScore: 75, maxRetries: 1);

        var nicheId = await SeedNicheAsync(provider);
        var productId = await GenerateAsync(provider, nicheId);

        // Drena apenas outline + chapters + review (sem outbox e sem jobs subsequentes)
        var worker = new JobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobWorker>.Instance);

        // Roda até Review ser processado (máx 10 jobs: outline + 2 capítulos + review)
        for (var i = 0; i < 10 && await worker.ProcessNextAsync(CancellationToken.None); i++) { }

        using var afterReview = provider.CreateScope();
        var dbAfterReview = afterReview.ServiceProvider.GetRequiredService<EbookDbContext>();

        // Cover NÃO enfileirado ainda (gate reprovou)
        Assert.False(await dbAfterReview.Jobs.AnyAsync(j => j.Type == ContentJobs.Cover));

        // Produto permanece em Review (não avançou)
        var productAfterReview = await dbAfterReview.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(ProductStage.Review, productAfterReview.Stage);

        // Evento ManuscriptAuditFailed no Outbox
        var outboxAfterReview = await dbAfterReview.OutboxEvents
            .Where(e => e.Type == nameof(ManuscriptAuditFailed))
            .ToListAsync();
        Assert.Single(outboxAfterReview);
    }

    // ── Cenário 4: tentativas esgotadas → avança mesmo com score baixo, sem loop ─────────────────

    [Fact]
    public async Task Tentativas_esgotadas_avancam_mesmo_com_score_baixo()
    {
        // maxRetries=0 → zero retries: na primeira rodada, se falhar, avança assim mesmo
        var (provider, _) = Build();
        using var _ = provider;
        await ConfigureSettingsAsync(provider, minScore: 75, maxRetries: 0);

        var nicheId = await SeedNicheAsync(provider);
        var productId = await GenerateAsync(provider, nicheId);
        await RunJobsAsync(provider); // pipeline completo

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

        // Avançou mesmo com score 72 < 75 (tentativas = 0, maxRetries = 0 → esgotado desde o início)
        Assert.Equal(ProductStage.Lp, product.Stage);

        // Nenhum Review retry foi criado (não entrou em loop)
        var retryJobs = await db.Jobs
            .Where(j => j.Type == ContentJobs.Review && j.IdempotencyKey!.Contains(":retry:"))
            .ToListAsync();
        Assert.Empty(retryJobs);
    }

    // ── Cenário 5: retry completo — gate falha, outbox processa, retry roda, esgota → avança ────

    [Fact]
    public async Task Ciclo_completo_retry_esgota_e_avanca_para_lp()
    {
        // maxRetries=1: 1 tentativa de retry disponível
        // FakeAi sempre retorna 72 < 75 → retry será re-executado, e na segunda vez esgota + avança
        var (provider, ai) = Build();
        using var _ = provider;
        await ConfigureSettingsAsync(provider, minScore: 75, maxRetries: 1);

        var nicheId = await SeedNicheAsync(provider);
        var productId = await GenerateAsync(provider, nicheId);

        // Fase 1: roda jobs (review falhará no gate, evento vai ao outbox)
        await RunJobsAsync(provider);

        using (var s = provider.CreateScope())
        {
            var db1 = s.ServiceProvider.GetRequiredService<EbookDbContext>();
            // Outbox tem o evento pendente (ainda não processado pelo dispatcher em background)
            var pending = await db1.OutboxEvents
                .Where(e => e.Type == nameof(ManuscriptAuditFailed) && e.ProcessedAtUtc == null)
                .CountAsync();
            // Se nenhum evento pendente: o dispatcher do background pode ter processado
            // Verificamos que o produto ainda não completou o pipeline OU já completou via retry
        }

        // Fase 2: dispara o outbox manualmente (simula o OutboxDispatcher em background)
        var dispatcher = BuildDispatcher(provider);
        await dispatcher.ProcessPendingOnceAsync(CancellationToken.None);

        // Fase 3: roda jobs novamente (processa o job de review retry)
        await RunJobsAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

        // Após 2 rodadas (original + retry), tentativas esgotadas → pipeline completo
        Assert.Equal(ProductStage.Lp, product.Stage);

        // Auditoria foi chamada 2 vezes (original + retry)
        Assert.Equal(2, ai.CallsFor("ebook.audit"));

        // Job de retry criado com a chave correta
        var retryJob = await db.Jobs
            .SingleOrDefaultAsync(j => j.Type == ContentJobs.Review && j.IdempotencyKey!.Contains(":retry:"));
        Assert.NotNull(retryJob);
        Assert.Equal(JobStatus.Succeeded, retryJob.Status);
    }
}
