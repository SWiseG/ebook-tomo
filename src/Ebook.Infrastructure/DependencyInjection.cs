using Ebook.Application.Administration.Auth;
using Ebook.Application.Administration.Dashboard;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Application.Discovery;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Ebook.Infrastructure.Administration;
using Ebook.Infrastructure.Ai;
using Ebook.Infrastructure.Content;
using Ebook.Infrastructure.Discovery;
using Ebook.Infrastructure.Events;
using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Jobs;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Scheduling;
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

        var dataRoot = configuration.GetSection(DataOptions.SectionName).Get<DataOptions>()?.RootPath ?? "./data";
        var dbDirectory = Path.Combine(dataRoot, "db");
        Directory.CreateDirectory(dbDirectory);
        services.AddDbContext<EbookDbContext>(o =>
            o.UseSqlite($"Data Source={Path.Combine(dbDirectory, "ebook.db")}"));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileStore, JsonFileStore>();
        services.AddSingleton<IArtifactStore, FileArtifactStore>();
        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        services.AddSingleton<IImageComposer, SkiaImageComposer>();
        services.AddSingleton<IPromptLibrary, PromptLibrary>();

        services.AddHttpClient<IPhotoProvider, PexelsPhotoProvider>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddSingleton<ClaudeCliClient>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtService, JwtService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJobQueue, JobQueue>();
        services.AddScoped<ISettingsStore, SettingsStore>();
        services.AddScoped<IDashboardReader, DashboardReader>();
        services.AddScoped<IProductReader, ProductReader>();
        services.AddScoped<IAiGateway, AiGateway>();

        services.AddScoped<INicheRepository, NicheRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<ITrendSnapshotRepository, TrendSnapshotRepository>();
        services.AddScoped<INicheReader, NicheReader>();

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
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }
}
