namespace Ebook.Domain.Optimization;

public interface IOptimizationRepository
{
    void AddRun(OptimizationRun run);

    void AddDecision(OptimizationDecision decision);

    Task<OptimizationRun?> GetRunByCycleAsync(int cycleNumber, CancellationToken ct = default);

    Task<OptimizationDecision?> GetDecisionAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<OptimizationDecision>> ListByRunAsync(Guid runId, CancellationToken ct = default);
}
