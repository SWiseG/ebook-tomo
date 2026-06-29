using Ebook.Application;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Application.Media;
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
/// Torneio de capas (A3): N candidatas são geradas, pontuadas por ScoreAsync, e a de maior
/// score é escolhida. tournamentSize=1 preserva o comportamento original (não-regressão).
/// </summary>
public class CoverTournamentTests
{
    /// <summary>
    /// IMediaGateway que só atende CoverWithText (para o torneio); falha para outros tipos.
    /// Retorna bytes únicos por chamada (primeiro byte = índice 1-based), permitindo identificar
    /// qual candidata foi armazenada após o torneio.
    /// </summary>
    private sealed class CoverMediaGateway : IMediaGateway
    {
        private int _coverCall;
        public int CoverCallCount => _coverCall;

        public Task<Result<MediaResult>> GenerateAsync(MediaBrief brief, CancellationToken ct = default)
        {
            if (brief.Kind != MediaKind.CoverWithText)
                return Task.FromResult(Result.Failure<MediaResult>(new Error("test.media", "só CoverWithText")));

            var idx = (byte)Interlocked.Increment(ref _coverCall); // 1, 2, 3…
            return Task.FromResult(Result.Success(new MediaResult([idx, 0xCC], MediaProvider.HuggingFace, false)));
        }
    }

    private static async Task<(ServiceProvider Provider, FakeImageComposer Img)> BuildAsync(
        ScoringFakeCoverQa qa,
        IMediaGateway media,
        int tournamentSize,
        bool aiFullCover = true)
    {
        var ai = new FakeAiGateway();
        var img = new FakeImageComposer();
        var provider = TestHost.Build(s =>
        {
            s.AddApplication();
            s.AddSingleton<IAiGateway>(ai);
            s.AddSingleton<IPdfRenderer>(new FakePdfRenderer());
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
            s.AddScoped<ILpVariantRepository, LpVariantRepository>();
            s.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
            s.AddScoped<ITrendSnapshotRepository, TrendSnapshotRepository>();
            s.AddScoped<ISocialPostRepository, SocialPostRepository>();
            s.AddScoped<IProductReader, ProductReader>();
            // Sobrescrever os defaults do TestHost com os fakes do torneio
            s.AddSingleton<ICoverQa>(qa);
            s.AddSingleton<IMediaGateway>(media);
            s.AddSingleton<IPromptLibrary, PassthroughPromptLibrary>(); // prompts "passam" para IA/media chegar
        });

        using var scope = provider.CreateScope();
        var settingsStore = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        await settingsStore.SetAsync(SettingKeys.CoverTournamentSize, tournamentSize);
        if (aiFullCover)
        {
            await settingsStore.SetAsync(SettingKeys.CoverAiFullCover, true);
        }

        return (provider, img);
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

    private static async Task RunPipelineAsync(ServiceProvider provider)
    {
        var worker = new JobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<JobWorker>>());

        for (var i = 0; i < 100 && await worker.ProcessNextAsync(CancellationToken.None); i++) { }
    }

    [Fact]
    public async Task Torneio_escolhe_candidata_com_maior_score()
    {
        // Scores por chamada: candidata 1 → 40, candidata 2 → 80, candidata 3 → 60
        var qa = new ScoringFakeCoverQa(40, 80, 60);
        var media = new CoverMediaGateway();
        var (provider, img) = await BuildAsync(qa, media, tournamentSize: 3);
        using var _ = provider;

        var nicheId = await SeedNicheAsync(provider);
        using var startScope = provider.CreateScope();
        var dispatcher = startScope.ServiceProvider.GetRequiredService<IDispatcher>();
        var result = await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Commercial));
        Assert.True(result.IsSuccess);

        await RunPipelineAsync(provider);

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var artifactStore = verify.ServiceProvider.GetRequiredService<IArtifactStore>();
        var product = await db.Products.AsNoTracking().SingleAsync();

        // Skia NÃO usada para capa — uma candidata IA venceu o torneio
        Assert.Equal(0, img.CoverCount);

        // 3 candidatas foram pontuadas
        Assert.Equal(3, qa.ScoreCalls);

        // Candidata vencedora (score 80) tem bytes [2, 0xCC] (2º índice no CoverMediaGateway)
        var coverBytes = await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug));
        Assert.NotNull(coverBytes);
        Assert.Equal(2, coverBytes![0]); // índice da candidata com score 80
    }

    [Fact]
    public async Task TournamentSize1_usa_caminho_original_sem_ScoreAsync()
    {
        // tournamentSize=1 → usa TryFullAiCoverAsync (ReviewAsync) em vez de torneio
        // Com CoverAiFullCover=true e ScoringFakeCoverQa (ReviewAsync sempre aprova),
        // a candidata IA é usada (bytes [1, 0xCC]), sem chamar ScoreAsync.
        var qa = new ScoringFakeCoverQa(99); // ReviewAsync aprova; ScoreAsync nunca deve ser chamado
        var media = new CoverMediaGateway();
        var (provider, img) = await BuildAsync(qa, media, tournamentSize: 1, aiFullCover: true);
        using var _ = provider;

        var nicheId = await SeedNicheAsync(provider);
        using var startScope = provider.CreateScope();
        var dispatcher = startScope.ServiceProvider.GetRequiredService<IDispatcher>();
        await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Commercial));

        await RunPipelineAsync(provider);

        // ScoreAsync NÃO foi chamado (caminho tournamentSize=1 usa ReviewAsync)
        Assert.Equal(0, qa.ScoreCalls);

        // Skia não usada — a candidata full-AI foi aprovada pelo ReviewAsync
        Assert.Equal(0, img.CoverCount);
    }

    [Fact]
    public async Task TournamentSize1_sem_aiFullCover_usa_Skia_comportamento_atual()
    {
        // tournamentSize=1, CoverAiFullCover=false (default) → exatamente o comportamento atual
        var qa = new ScoringFakeCoverQa();
        var media = new CoverMediaGateway();
        var (provider, img) = await BuildAsync(qa, media, tournamentSize: 1, aiFullCover: false);
        using var _ = provider;

        var nicheId = await SeedNicheAsync(provider);
        using var startScope = provider.CreateScope();
        var dispatcher = startScope.ServiceProvider.GetRequiredService<IDispatcher>();
        await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Commercial));

        await RunPipelineAsync(provider);

        // Skia usada (CoverAiFullCover=false → TryFullAiCoverAsync retorna null)
        Assert.Equal(1, img.CoverCount);
        // ScoreAsync nunca chamado
        Assert.Equal(0, qa.ScoreCalls);
        // Media gateway nunca chamado para capa
        Assert.Equal(0, media.CoverCallCount);
    }

    [Fact]
    public async Task Quota_esgotada_fallback_Skia()
    {
        // tournamentSize=3, mas o gateway falha para todas as chamadas CoverWithText
        var qa = new ScoringFakeCoverQa();
        var media = new FakeMediaGateway(); // sempre retorna falha
        var (provider, img) = await BuildAsync(qa, media, tournamentSize: 3, aiFullCover: true);
        using var _ = provider;

        var nicheId = await SeedNicheAsync(provider);
        using var startScope = provider.CreateScope();
        var dispatcher = startScope.ServiceProvider.GetRequiredService<IDispatcher>();
        await dispatcher.SendAsync(new GenerateProductCommand(nicheId, null, QualityTier.Commercial));

        await RunPipelineAsync(provider);

        // Todas as candidatas falharam → Skia fallback
        Assert.Equal(1, img.CoverCount);
        // ScoreAsync nunca chamado (nenhuma candidata gerada)
        Assert.Equal(0, qa.ScoreCalls);
    }
}
