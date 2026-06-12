using Ebook.Application.Common.Jobs;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Jobs;

/// <summary>
/// Worker sequencial da fila de jobs (SQLite). Retry exponencial com jitter;
/// 3 falhas → Dead (dead-letter visível no painel, retry manual).
/// Sequencial por design: Claude CLI e Playwright exigem concorrência 1.
/// </summary>
public sealed class JobWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<JobWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("JobWorker iniciado");
        while (!stoppingToken.IsCancellationRequested)
        {
            bool processed;
            try
            {
                processed = await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no loop do JobWorker");
                processed = false;
            }

            if (!processed)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Processa o próximo job pendente; público para testes de integração.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var now = DateTime.UtcNow;

        var job = await db.Jobs
            .Where(j => j.Status == JobStatus.Pending && j.ScheduledAtUtc <= now)
            .OrderBy(j => j.ScheduledAtUtc)
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            return false;
        }

        job.Status = JobStatus.Running;
        job.StartedAtUtc = now;
        job.Attempts++;
        await db.SaveChangesAsync(ct);

        var handler = scope.ServiceProvider.GetServices<IJobHandler>()
            .FirstOrDefault(h => h.Type == job.Type);

        if (handler is null)
        {
            job.Status = JobStatus.Dead;
            job.LastError = $"Nenhum IJobHandler registrado para o tipo '{job.Type}'.";
            job.FinishedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogError("Job {JobId} sem handler para tipo {JobType}", job.Id, job.Type);
            return true;
        }

        try
        {
            var result = await handler.ExecuteAsync(job.PayloadJson, ct);
            if (result.IsSuccess)
            {
                job.Status = JobStatus.Succeeded;
                job.LastError = null;
            }
            else
            {
                Reschedule(job, result.Error.ToString());
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown no meio do job: volta para Pending para reexecução (handlers são idempotentes)
            job.Status = JobStatus.Pending;
            job.Attempts--;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} ({JobType}) lançou exceção na tentativa {Attempt}",
                job.Id, job.Type, job.Attempts);
            Reschedule(job, ex.Message);
        }

        job.FinishedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    private void Reschedule(JobRecord job, string error)
    {
        job.LastError = error;
        if (job.Attempts >= MaxAttempts)
        {
            job.Status = JobStatus.Dead;
            logger.LogError("Job {JobId} ({JobType}) movido para dead-letter: {Error}", job.Id, job.Type, error);
            return;
        }

        var backoff = TimeSpan.FromSeconds(30 * Math.Pow(2, job.Attempts) + Random.Shared.Next(0, 15));
        job.Status = JobStatus.Pending;
        job.ScheduledAtUtc = DateTime.UtcNow.Add(backoff);
        logger.LogWarning("Job {JobId} ({JobType}) reagendado para {ScheduledAt} após falha: {Error}",
            job.Id, job.Type, job.ScheduledAtUtc, error);
    }
}
