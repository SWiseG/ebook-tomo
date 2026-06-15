using Ebook.Application.Analytics;
using Ebook.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Ebook.Infrastructure.Scheduling;

/// <summary>
/// Cron diário (E11-02): agrega os eventos brutos + vendas em MetricDaily. Roda a agregação
/// direto (trabalho leve) para hoje e ontem — upsert idempotente garante recálculo correto.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MetricsAggregationJob(
    IMetricsAggregator aggregator,
    IClock clock,
    ILogger<MetricsAggregationJob> logger) : IJob
{
    public const string JobName = "metrics-daily";

    public async Task Execute(IJobExecutionContext context)
    {
        var today = clock.UtcNow.Date;
        var rows = await aggregator.AggregateAsync(today, context.CancellationToken);
        rows += await aggregator.AggregateAsync(today.AddDays(-1), context.CancellationToken);
        logger.LogInformation("Agregação de métricas concluída: {Rows} linhas (hoje + ontem)", rows);
    }
}
