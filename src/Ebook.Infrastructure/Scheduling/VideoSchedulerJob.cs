using System.Globalization;
using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Video;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Ebook.Infrastructure.Scheduling;

/// <summary>
/// Cron semanal (E10): para cada produto Live, enfileira a geração de um Reel — se
/// <c>video.enabled</c> estiver ligado (exige Piper+FFmpeg). Chave por produto+semana evita duplicar.
/// </summary>
[DisallowConcurrentExecution]
public sealed class VideoSchedulerJob(
    IProductRepository products,
    ISettingsStore settings,
    IJobQueue jobQueue,
    IClock clock,
    ILogger<VideoSchedulerJob> logger) : IJob
{
    public const string JobName = "video-scheduler";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var enabled = await settings.GetOrDefaultAsync(SettingKeys.VideoEnabled, false, ct);
        if (!enabled)
        {
            return;
        }

        var week = ISOWeek.GetWeekOfYear(clock.UtcNow);
        var live = await products.ListByStatusAsync(ProductStatus.Synchronized, ct);
        foreach (var product in live)
        {
            await jobQueue.EnqueueAsync(new JobRequest(
                VideoJobs.Generate,
                JsonSerializer.Serialize(new VideoJobPayload(product.Id), JsonOptions),
                VideoJobs.GenerateKey(product.Id, week),
                ProductId: product.Id), ct);
        }

        logger.LogInformation("Cron de vídeo: {Count} Reels enfileirados (semana {Week})", live.Count, week);
    }
}
