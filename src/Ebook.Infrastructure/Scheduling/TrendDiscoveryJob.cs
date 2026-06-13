using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Discovery;
using Ebook.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Ebook.Infrastructure.Scheduling;

/// <summary>
/// Cron de descoberta (E02-06, ciclo de ~30 dias): enfileira o job trends.discover.
/// O trabalho pesado (rede + score) roda na fila com retry; a chave por ciclo evita duplicação.
/// </summary>
[DisallowConcurrentExecution]
public sealed class TrendDiscoveryJob(
    IJobQueue jobQueue,
    IClock clock,
    ILogger<TrendDiscoveryJob> logger) : IJob
{
    public const string JobName = "trend-discovery";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Execute(IJobExecutionContext context)
    {
        var cycle = (clock.UtcNow.Year - 2026) * 12 + clock.UtcNow.Month;
        await jobQueue.EnqueueAsync(new JobRequest(
            DiscoveryJobs.Discover,
            JsonSerializer.Serialize(new DiscoverNichesJobPayload(), JsonOptions),
            DiscoveryJobs.DiscoverKey(cycle)), context.CancellationToken);

        logger.LogInformation("Cron de descoberta: job trends.discover enfileirado para o ciclo {Cycle}", cycle);
    }
}
