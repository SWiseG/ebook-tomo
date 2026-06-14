using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Social;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Social;

/// <summary>
/// Cron diário (E08-03): pega os posts vencidos e ainda planejados, marca-os como Queued
/// e enfileira a publicação de cada um — desde que <c>social.autoPublish</c> esteja ligado.
/// Caso contrário, não faz nada (os posts ficam agendados e visíveis no painel).
/// </summary>
public sealed class DispatchDuePostsJobHandler(
    ISocialPostRepository posts,
    ISettingsStore settings,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<DispatchDuePostsJobHandler> logger) : IJobHandler
{
    private const int BatchSize = 25;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => SocialJobs.Dispatch;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var autoPublish = await settings.GetOrDefaultAsync(SettingKeys.SocialAutoPublish, false, ct);
        if (!autoPublish)
        {
            logger.LogInformation("Dispatch social ignorado (social.autoPublish=false); posts permanecem agendados.");
            return Result.Success();
        }

        var due = await posts.GetDueAsync(clock.UtcNow, BatchSize, ct);
        var dispatched = 0;
        foreach (var post in due)
        {
            if (post.Queue().IsFailure)
            {
                continue;
            }

            await jobQueue.EnqueueAsync(new JobRequest(
                SocialJobs.Publish,
                JsonSerializer.Serialize(new PublishPostJobPayload(post.Id), JsonOptions),
                SocialJobs.PublishKey(post.Id),
                ProductId: post.ProductId), ct);
            dispatched++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        if (dispatched > 0)
        {
            logger.LogInformation("Dispatch social: {Count} posts enfileirados para publicação", dispatched);
        }

        return Result.Success();
    }
}
