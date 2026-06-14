using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Social;
using Ebook.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Ebook.Infrastructure.Scheduling;

/// <summary>
/// Cron diário (E08-03): enfileira o job social.dispatch, que publica os posts vencidos do
/// calendário (quando social.autoPublish estiver ligado). Chave por dia evita duplicação.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SocialSchedulerJob(
    IJobQueue jobQueue,
    IClock clock,
    ILogger<SocialSchedulerJob> logger) : IJob
{
    public const string JobName = "social-scheduler";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Execute(IJobExecutionContext context)
    {
        var now = clock.UtcNow;
        await jobQueue.EnqueueAsync(new JobRequest(
            SocialJobs.Dispatch,
            JsonSerializer.Serialize(new DispatchJobPayload(now), JsonOptions),
            SocialJobs.DispatchKey(now)), context.CancellationToken);

        logger.LogInformation("Cron social: job social.dispatch enfileirado para {Day}", now.Date);
    }
}
