using Ebook.Domain.Common;
using Ebook.Domain.Optimization;

namespace Ebook.Application.Optimization;

/// <summary>
/// Executa um ciclo de otimização (E12): classifica os produtos Live e propõe decisões.
/// Implementado na camada de aplicação; chamado pelo cron de 30 dias e pelo gatilho manual.
/// </summary>
public interface IOptimizationService
{
    Task<Result<Guid>> RunCycleAsync(CancellationToken ct = default);
}

/// <summary>
/// Executa uma decisão já aprovada (Kill → arquiva + repõe; Iterate → itera + novo preço; etc.),
/// marcando-a como Executed. Não persiste — o chamador salva via IUnitOfWork.
/// </summary>
public interface IOptimizationExecutor
{
    Task<Result> ExecuteAsync(OptimizationDecision decision, CancellationToken ct = default);
}

public static class OptimizationPaths
{
    public static string Report(int cycle) => $"cycles/cycle-{cycle}-report.json";
}
