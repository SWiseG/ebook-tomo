using Ebook.Application.Administration.Auth;
using Ebook.Application.Administration.Dashboard;
using Ebook.Application.Administration.Media;
using Ebook.Application.Administration.Provenance;
using Ebook.Application.Administration.Sources;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Application.Analytics;
using Ebook.Application.Knowledge;
using Ebook.Application.Discovery;
using Ebook.Application.Media;
using Ebook.Application.Optimization;
using Ebook.Application.Publishing;
using Ebook.Application.Social;
using Ebook.Application.Video;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Optimization;
using Ebook.Domain.Products;
using Ebook.Domain.Sales;
using Ebook.Domain.Social;
using Ebook.Infrastructure.Administration;
using Ebook.Infrastructure.Ai;
using Ebook.Infrastructure.Analytics;
using Ebook.Infrastructure.Content;
using Ebook.Infrastructure.Discovery;
using Ebook.Infrastructure.Events;
using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Jobs;
using Ebook.Infrastructure.Optimization;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Publishing;
using Ebook.Infrastructure.Scheduling;
using Ebook.Infrastructure.Social;
using Ebook.Infrastructure.Video;
using Ebook.Infrastructure.Security;
using Ebook.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Ebook.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminAuthOptions>(configuration.GetSection(AdminAuthOptions.SectionName));
        services.Configure<PexelsOptions>(configuration.GetSection(PexelsOptions.SectionName));
        services.Configure<Media.UnsplashOptions>(configuration.GetSection(Media.UnsplashOptions.SectionName));
        services.Configure<Media.PixabayOptions>(configuration.GetSection(Media.PixabayOptions.SectionName));
        services.Configure<Media.MediaOptions>(configuration.GetSection(Media.MediaOptions.SectionName));
        services.Configure<KiwifyOptions>(configuration.GetSection(KiwifyOptions.SectionName));
        services.Configure<MetaOptions>(configuration.GetSection(MetaOptions.SectionName));
        services.Configure<VideoOptions>(configuration.GetSection(VideoOptions.SectionName));

        var dataRoot = configuration.GetSection(DataOptions.SectionName).Get<DataOptions>()?.RootPath ?? "./data";
        var dbDirectory = Path.Combine(dataRoot, "db");
        Directory.CreateDirectory(dbDirectory);
        services.AddDbContext<EbookDbContext>(o =>
            o.UseSqlite($"Data Source={Path.Combine(dbDirectory, "ebook.db")}"));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileStore, JsonFileStore>();
        services.AddSingleton<IArtifactStore, FileArtifactStore>();
        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        services.AddSingleton<IEbookExporter, EpubRenderer>();
        services.AddSingleton<IDocxExporter, DocxRenderer>();
        services.AddSingleton<IImageComposer, SkiaImageComposer>();
        services.AddSingleton<IPromptLibrary, PromptLibrary>();

        // Media Gateway (E14): cadeia free-first de geração de imagem por trás do seam IPhotoProvider.
        // Pexels deixa de ser o IPhotoProvider direto e vira o último elo da cadeia (banco de fotos).
        services.AddHttpClient<PexelsPhotoProvider>(c => c.Timeout = TimeSpan.FromSeconds(15));
        // ordem de registro = ordem da cadeia (qualidade-primeiro dentro do grátis):
        // generativos premium (com chave) → bancos de foto (com chave) → Pollinations (grátis) → Skia (piso).
        services.AddHttpClient<IMediaResolver, Media.GeminiImageResolver>(c => c.Timeout = TimeSpan.FromSeconds(90));
        services.AddHttpClient<IMediaResolver, Media.HiggsfieldImageResolver>(c => c.Timeout = TimeSpan.FromSeconds(60));
        services.AddHttpClient<IMediaResolver, Media.CloudflareImageResolver>(c => c.Timeout = TimeSpan.FromSeconds(90));
        services.AddHttpClient<IMediaResolver, Media.HuggingFaceImageResolver>(c => c.Timeout = TimeSpan.FromSeconds(90));
        services.AddScoped<IMediaResolver, Media.PexelsMediaResolver>();
        services.AddHttpClient<IMediaResolver, Media.UnsplashMediaResolver>(c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient<IMediaResolver, Media.PixabayMediaResolver>(c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient<IMediaResolver, Media.PollinationsMediaResolver>(c => c.Timeout = TimeSpan.FromSeconds(60));
        services.AddSingleton<IMediaResolver, Media.LocalSkiaImageResolver>(); // piso garantido — nunca falha
        services.AddScoped<IMediaGateway, Media.MediaGateway>();
        services.AddScoped<IPhotoProvider, Media.MediaGatewayPhotoProvider>();
        services.AddSingleton<ClaudeCliClient>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtService, JwtService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJobQueue, JobQueue>();
        services.AddScoped<ISettingsStore, SettingsStore>();
        services.AddScoped<IDashboardReader, DashboardReader>();
        services.AddScoped<IMediaTelemetryReader, MediaTelemetryReader>();
        services.AddScoped<ISourcesTelemetryReader, SourcesTelemetryReader>();
        services.AddScoped<IProductProvenanceReader, ProductProvenanceReader>();
        services.AddScoped<IProductReader, ProductReader>();
        services.AddScoped<IAiGateway, AiGateway>();

        services.AddScoped<INicheRepository, NicheRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<ILpVariantRepository, LpVariantRepository>();
        services.AddSingleton<IRandom, SystemRandom>();
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<ITrendSnapshotRepository, TrendSnapshotRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISocialPostRepository, SocialPostRepository>();
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<INicheReader, NicheReader>();
        services.AddScoped<IAnalyticsRecorder, AnalyticsRecorder>();
        services.AddScoped<IMetricsAggregator, MetricsAggregator>();
        services.AddScoped<IMetricsReader, MetricsReader>();
        services.AddScoped<IOptimizationRepository, OptimizationRepository>();
        services.AddScoped<IOptimizationReader, OptimizationReader>();
        services.AddSingleton<IKiwifyPublisher, KiwifyPublisher>();
        services.AddHttpClient(KiwifyApiClient.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddSingleton<IKiwifyCatalog, KiwifyApiClient>();
        services.AddHttpClient<ISocialPublisher, MetaGraphPublisher>(c => c.Timeout = TimeSpan.FromSeconds(60));
        services.AddSingleton<ITtsEngine, PiperTtsEngine>();
        services.AddSingleton<IVideoComposer, FfmpegVideoComposer>();
        services.AddScoped<IStyleAnalyzer, ClaudeVisionStyleAnalyzer>();
        services.AddScoped<Application.Content.Images.ICoverQa, ClaudeVisionCoverQa>();

        // fontes de tendência (E02): client nomeado compartilhado + múltiplas implementações de ITrendSource
        services.AddHttpClient("trends", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("ebook-platform/1.0 (+discovery)");
        });
        services.AddSingleton<ITrendSource, RedditTrendSource>();
        services.AddSingleton<ITrendSource, GoogleAutocompleteTrendSource>();

        // ordem de registro = ordem da cadeia de resolução do AI Gateway
        services.AddScoped<IAiResolver, AiCacheResolver>();
        services.AddScoped<IAiResolver, ClaudeCliResolver>();

        // E15 — loop de aprendizado de estilo: análise de capa por visão (Claude CLI), sem cache de IA
        services.AddScoped<IStyleAnalyzer, ClaudeVisionStyleAnalyzer>();

        services.AddSingleton<OutboxDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxDispatcher>());
        services.AddSingleton<JobWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<JobWorker>());

        services.AddQuartz(quartz =>
        {
            var cron = configuration["Scheduling:HousekeepingCron"] ?? "0 0 3 * * ?";
            var jobKey = new JobKey(HousekeepingJob.JobName);
            quartz.AddJob<HousekeepingJob>(o => o.WithIdentity(jobKey));
            quartz.AddTrigger(t => t
                .ForJob(jobKey)
                .WithIdentity(HousekeepingJob.JobName + "-trigger")
                .WithCronSchedule(cron));

            // descoberta de nichos: ciclo de ~30 dias (mensal por padrão)
            var discoveryCron = configuration["Scheduling:DiscoveryCron"] ?? "0 0 4 1 * ?";
            var discoveryKey = new JobKey(TrendDiscoveryJob.JobName);
            quartz.AddJob<TrendDiscoveryJob>(o => o.WithIdentity(discoveryKey));
            quartz.AddTrigger(t => t
                .ForJob(discoveryKey)
                .WithIdentity(TrendDiscoveryJob.JobName + "-trigger")
                .WithCronSchedule(discoveryCron));

            // calendário social: cron diário publica os posts vencidos (E08-03)
            var socialCron = configuration["Scheduling:SocialCron"] ?? "0 0 13 * * ?";
            var socialKey = new JobKey(SocialSchedulerJob.JobName);
            quartz.AddJob<SocialSchedulerJob>(o => o.WithIdentity(socialKey));
            quartz.AddTrigger(t => t
                .ForJob(socialKey)
                .WithIdentity(SocialSchedulerJob.JobName + "-trigger")
                .WithCronSchedule(socialCron));

            // métricas: agregação diária do funil (E11-02)
            var metricsCron = configuration["Scheduling:MetricsCron"] ?? "0 30 2 * * ?";
            var metricsKey = new JobKey(MetricsAggregationJob.JobName);
            quartz.AddJob<MetricsAggregationJob>(o => o.WithIdentity(metricsKey));
            quartz.AddTrigger(t => t
                .ForJob(metricsKey)
                .WithIdentity(MetricsAggregationJob.JobName + "-trigger")
                .WithCronSchedule(metricsCron));

            // otimização de ROI: ciclo de ~30 dias (E12)
            var optimizeCron = configuration["Scheduling:OptimizeCron"] ?? "0 0 5 1 * ?";
            var optimizeKey = new JobKey(OptimizeCycleJob.JobName);
            quartz.AddJob<OptimizeCycleJob>(o => o.WithIdentity(optimizeKey));
            quartz.AddTrigger(t => t
                .ForJob(optimizeKey)
                .WithIdentity(OptimizeCycleJob.JobName + "-trigger")
                .WithCronSchedule(optimizeCron));

            // vídeo: cron semanal gera Reels por produto ativo (E10), gated por video.enabled
            var videoCron = configuration["Scheduling:VideoCron"] ?? "0 0 14 ? * MON";
            var videoKey = new JobKey(VideoSchedulerJob.JobName);
            quartz.AddJob<VideoSchedulerJob>(o => o.WithIdentity(videoKey));
            quartz.AddTrigger(t => t
                .ForJob(videoKey)
                .WithIdentity(VideoSchedulerJob.JobName + "-trigger")
                .WithCronSchedule(videoCron));

            // aprendizado de estilo (E15): cron semanal analisa capas por nicho, gated por style.learn.enabled
            var styleCron = configuration["Scheduling:StyleLearnCron"] ?? "0 0 6 ? * MON";
            var styleKey = new JobKey(StyleLearnJob.JobName);
            quartz.AddJob<StyleLearnJob>(o => o.WithIdentity(styleKey));
            quartz.AddTrigger(t => t
                .ForJob(styleKey)
                .WithIdentity(StyleLearnJob.JobName + "-trigger")
                .WithCronSchedule(styleCron));
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }
}
