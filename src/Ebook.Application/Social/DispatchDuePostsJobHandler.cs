using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Social;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Social;

/// <summary>
/// Cron diário (E08-03): pega os posts vencidos, marca-os como Queued e enfileira a publicação.
/// Gate de aprovação: por padrão só despacha posts <b>aprovados</b> no painel; com
/// <c>social.autoPublish</c> ligado (auto-aprovar) despacha todos os vencidos sem revisão.
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
        // auto-aprovar (social.autoPublish) pula o gate; senão só despacha os aprovados no painel.
        var autoApprove = await settings.GetOrDefaultAsync(SettingKeys.SocialAutoPublish, false, ct);
        var due = await posts.GetDueAsync(clock.UtcNow, BatchSize, approvedOnly: !autoApprove, ct);
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
