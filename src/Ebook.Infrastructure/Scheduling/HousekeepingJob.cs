using Ebook.Domain.Abstractions;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Ebook.Infrastructure.Scheduling;

/// <summary>
/// Cron diário de retenção: outbox processado > 30d, jobs Succeeded > 30d,
/// cache de IA sem hit > 180d. Registra a execução em JobRunLog (base do
/// catch-up dos ciclos de 30 dias que entram na Fase 1).
/// </summary>
[DisallowConcurrentExecution]
public sealed class HousekeepingJob(
    EbookDbContext db,
    IClock clock,
    ILogger<HousekeepingJob> logger) : IJob
{
    public const string JobName = "housekeeping";

    public async Task Execute(IJobExecutionContext context)
    {
        var now = clock.UtcNow;
        var outboxCutoff = now.AddDays(-30);
        var jobsCutoff = now.AddDays(-30);
        var cacheCutoff = now.AddDays(-180);

        var outboxRemoved = await db.OutboxEvents
            .Where(e => e.ProcessedAtUtc != null && e.ProcessedAtUtc < outboxCutoff)
            .ExecuteDeleteAsync(context.CancellationToken);

        var jobsRemoved = await db.Jobs
            .Where(j => j.Status == JobStatus.Succeeded && j.FinishedAtUtc < jobsCutoff)
            .ExecuteDeleteAsync(context.CancellationToken);

        var cacheExpired = await db.AiCache
            .Where(c => (c.LastHitAtUtc ?? c.CreatedAtUtc) < cacheCutoff)
            .ExecuteDeleteAsync(context.CancellationToken);

        var runLog = await db.JobRunLogs.FirstOrDefaultAsync(r => r.JobName == JobName, context.CancellationToken);
        var detail = $"outbox={outboxRemoved}, jobs={jobsRemoved}, aiCache={cacheExpired}";
        if (runLog is null)
        {
            db.JobRunLogs.Add(new JobRunLogRecord { JobName = JobName, LastRunAtUtc = now, Detail = detail });
        }
        else
        {
            runLog.LastRunAtUtc = now;
            runLog.Detail = detail;
        }

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Housekeeping concluído: {Detail}", detail);
    }
}
