using Ebook.Application;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
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

namespace Ebook.Infrastructure.Tests.Video;

/// <summary>E10 — geração de Reel: roteiro (IA) → cards 9:16 → narração → MP4 → Artifact(Video) + SocialPost(Reel).</summary>
public class VideoTests
{
    private static (ServiceProvider Provider, FakeAiGateway Ai, FakeImageComposer Img, FakeVideoComposer Video) Build()
    {
        var ai = new FakeAiGateway();
        var img = new FakeImageComposer();
        var video = new FakeVideoComposer();
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
            s.AddSingleton<IVideoComposer>(video);
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
        return (provider, ai, img, video);
    }

    private static async Task<Guid> SeedLiveProductAsync(ServiceProvider provider, string slug = "guia-video")
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var now = DateTime.UtcNow;
        var product = Product.Create(Guid.NewGuid(), slug, "Guia Vídeo", QualityTier.Commercial, now);
        product.SetSalesCopy("{\"headline\":\"Assuma o controle\"}");
        product.AdvanceStage();
        product.AdvanceStage();
        product.AdvanceStage();
        product.AdvanceStage();
        product.SubmitForApproval();
        product.Approve();
        product.SetCheckoutLink("https://pay/x");
        product.MarkPublished(PublicationPlatform.Kiwify, now);
        product.MarkSynchronized("kw-x");
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task EnqueueAsync(ServiceProvider provider, JobRequest request)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync(request);
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

    private static string Payload(Guid id) =>
        System.Text.Json.JsonSerializer.Serialize(new VideoJobPayload(id));

    [Fact]
    public async Task Gera_reel_com_cards_narracao_artefato_e_post()
    {
        var (provider, ai, img, video) = Build();
        using var _p = provider;
        var id = await SeedLiveProductAsync(provider);

        await EnqueueAsync(provider, new JobRequest(VideoJobs.Generate, Payload(id), VideoJobs.GenerateKey(id, 1), id));
        await DrainAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var artifactStore = scope.ServiceProvider.GetRequiredService<IArtifactStore>();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();

        Assert.Equal(1, ai.CallsFor("video.script"));
        Assert.Equal(3, img.SocialCount); // um card 9:16 por cena
        Assert.Equal(3, video.LastSceneCount);
        Assert.Equal(1, video.Count);

        Assert.NotNull(await artifactStore.ReadBytesAsync(ContentPaths.VideoReel("guia-video", 1)));
        Assert.NotNull(await fileStore.ReadTextAsync(ContentPaths.VideoScript("guia-video")));
        Assert.Equal(1, await db.Artifacts.CountAsync(a => a.Type == ArtifactType.Video));

        var reel = await db.SocialPosts.AsNoTracking().SingleAsync(p => p.PostType == SocialPostType.Reel);
        Assert.Equal(SocialNetwork.Instagram, reel.Network);
        Assert.Contains("Link na bio", reel.Caption, StringComparison.Ordinal);
        Assert.NotNull(reel.MediaPath);
    }

    [Fact]
    public async Task Reexecutar_e_idempotente()
    {
        var (provider, ai, _, video) = Build();
        using var _p = provider;
        var id = await SeedLiveProductAsync(provider);

        await EnqueueAsync(provider, new JobRequest(VideoJobs.Generate, Payload(id), VideoJobs.GenerateKey(id, 1), id));
        await DrainAsync(provider);
        await EnqueueAsync(provider, new JobRequest(VideoJobs.Generate, Payload(id), VideoJobs.GenerateKey(id, 1) + ":2", id));
        await DrainAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(1, ai.CallsFor("video.script")); // não regerou
        Assert.Equal(1, video.Count);
        Assert.Equal(1, await db.Artifacts.CountAsync(a => a.Type == ArtifactType.Video));
        Assert.Equal(1, await db.SocialPosts.CountAsync(p => p.PostType == SocialPostType.Reel));
    }
}
