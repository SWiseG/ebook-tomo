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
using Ebook.Domain.Common;
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
/// Testes do passe de coesão (A1): bridges inseridas ao fim de cada capítulo,
/// remoções por correspondência exata e idempotência na re-entrega.
/// </summary>
public class ContinuityPassTests
{
    private const string BridgeCap1 = "Agora que você mapeou o terreno, é hora de construir a sua reserva.";
    private const string BridgeCap2 = "Com a reserva formada, o próximo passo é investir com confiança.";
    private const string DuplicateText = "Segundo parágrafo com exemplo prático.";

    private const string ContinuityJson = $$"""
        {
          "bridges": [
            { "chapterN": 1, "text": "{{BridgeCap1}}" },
            { "chapterN": 2, "text": "{{BridgeCap2}}" }
          ],
          "removals": [
            { "text": "{{DuplicateText}}" }
          ],
          "hookFixes": []
        }
        """;

    private static (ServiceProvider Provider, OverrideAiGateway Ai) Build(string continuityJson)
    {
        var ai = new OverrideAiGateway(continuityJson);
        var provider = TestHost.Build(s =>
        {
            s.AddApplication();
            s.AddSingleton<IAiGateway>(ai);
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

    private static async Task RunJobsAsync(ServiceProvider provider)
    {
        var worker = new JobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<JobWorker>>());

        for (var i = 0; i < 100 && await worker.ProcessNextAsync(CancellationToken.None); i++) { }
    }

    [Fact]
    public async Task Bridges_inseridas_ao_fim_de_cada_capitulo()
    {
        var (provider, _) = Build(ContinuityJson);
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Commercial));
        }

        await RunJobsAsync(provider);

        using var verify = provider.CreateScope();
        var fileStore = verify.ServiceProvider.GetRequiredService<IFileStore>();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync();

        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1));
        Assert.NotNull(manuscript);

        // Bridges inseridas ao fim de cada capítulo
        Assert.Contains(BridgeCap1, manuscript, StringComparison.Ordinal);
        Assert.Contains(BridgeCap2, manuscript, StringComparison.Ordinal);

        // Bridge do capítulo 1 aparece ANTES do heading do capítulo 2
        var bridgeIdx = manuscript.IndexOf(BridgeCap1, StringComparison.Ordinal);
        var cap2Idx = manuscript.IndexOf("## Capítulo 2 — ", StringComparison.Ordinal);
        Assert.True(bridgeIdx < cap2Idx, "Bridge do cap 1 deve preceder o heading do cap 2.");
    }

    [Fact]
    public async Task Remocao_por_correspondencia_exata_elimina_primeira_ocorrencia()
    {
        var (provider, _) = Build(ContinuityJson);
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Commercial));
        }

        await RunJobsAsync(provider);

        using var verify = provider.CreateScope();
        var fileStore = verify.ServiceProvider.GetRequiredService<IFileStore>();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync();

        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1));
        Assert.NotNull(manuscript);

        // O texto duplicado deve aparecer no máximo 1× após a remoção (havia 2×)
        var occurrences = CountOccurrences(manuscript!, DuplicateText);
        Assert.True(occurrences <= 1, $"Esperado ≤1 ocorrência de '{DuplicateText}', encontrado: {occurrences}");
    }

    [Fact]
    public async Task Re_entrega_nao_duplica_bridges()
    {
        var (provider, ai) = Build(ContinuityJson);
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Commercial));
        }

        await RunJobsAsync(provider);

        // Força re-entrega de todos os jobs
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
            await db.Jobs.ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Pending)
                .SetProperty(j => j.Attempts, 0)
                .SetProperty(j => j.ScheduledAtUtc, DateTime.UtcNow.AddSeconds(-1)));
        }

        var continuityCallsBefore = ai.ContinuityCalls;
        await RunJobsAsync(provider);

        // Continuidade não foi re-chamada (marker existe)
        Assert.Equal(continuityCallsBefore, ai.ContinuityCalls);

        // Bridges não duplicadas no manuscrito
        using var verify = provider.CreateScope();
        var fileStore = verify.ServiceProvider.GetRequiredService<IFileStore>();
        var db2 = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db2.Products.AsNoTracking().SingleAsync();

        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1));
        Assert.NotNull(manuscript);

        Assert.Equal(1, CountOccurrences(manuscript!, BridgeCap1));
        Assert.Equal(1, CountOccurrences(manuscript!, BridgeCap2));
    }

    [Fact]
    public async Task Draft_nao_executa_passe_de_continuidade()
    {
        var (provider, ai) = Build(ContinuityJson);
        using var _ = provider;
        var nicheId = await SeedNicheAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Draft));
        }

        await RunJobsAsync(provider);

        Assert.Equal(0, ai.ContinuityCalls);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }

        return count;
    }

    /// <summary>
    /// Gateway de IA que delega para o <see cref="FakeAiGateway"/> base mas substitui
    /// a resposta de "ebook.continuity" pelo JSON configurado no teste.
    /// </summary>
    private sealed class OverrideAiGateway(string continuityJson) : IAiGateway
    {
        private readonly FakeAiGateway _base = new();
        private int _continuityCalls;

        public int ContinuityCalls => _continuityCalls;

        public Task<Result<AiResponse>> CompleteAsync(AiRequest request, CancellationToken ct = default)
        {
            if (request.Purpose != "ebook.continuity") return _base.CompleteAsync(request, ct);

            Interlocked.Increment(ref _continuityCalls);
            return Task.FromResult(Result.Success(
                new AiResponse(continuityJson, AiProviderKind.ClaudeCli, CacheHit: false, DurationMs: 0)));
        }
    }
}
