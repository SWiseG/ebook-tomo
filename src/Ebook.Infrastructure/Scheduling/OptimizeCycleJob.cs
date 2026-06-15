using Ebook.Application.Optimization;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Ebook.Infrastructure.Scheduling;

/// <summary>
/// Cron de ~30 dias (E12): roda o ciclo de otimização direto (trabalho leve, sem IA por padrão).
/// Idempotente por ciclo. Decisões ficam Proposed para veto humano, salvo roi.autoExecute.
/// </summary>
[DisallowConcurrentExecution]
public sealed class OptimizeCycleJob(
    IOptimizationService optimization,
    ILogger<OptimizeCycleJob> logger) : IJob
{
    public const string JobName = "optimize-cycle";

    public async Task Execute(IJobExecutionContext context)
    {
        var result = await optimization.RunCycleAsync(context.CancellationToken);
        if (result.IsFailure)
        {
            logger.LogError("Ciclo de otimização falhou: {Error}", result.Error.Code);
            return;
        }

        logger.LogInformation("Ciclo de otimização concluído (run {RunId})", result.Value);
    }
}
