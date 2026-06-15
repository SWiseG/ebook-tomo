using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;

namespace Ebook.Application.Optimization;

public sealed record OptimizationRunDto(
    Guid Id,
    int CycleNumber,
    DateTime ExecutedAtUtc,
    string Status,
    int DecisionCount);

public sealed record OptimizationDecisionDto(
    Guid Id,
    Guid ProductId,
    string ProductTitle,
    string Decision,
    string Status,
    string Rationale,
    string ActionsJson);

/// <summary>Leitura das execuções/decisões do otimizador para o painel.</summary>
public interface IOptimizationReader
{
    Task<IReadOnlyList<OptimizationRunDto>> ListRunsAsync(CancellationToken ct);
    Task<IReadOnlyList<OptimizationDecisionDto>> ListDecisionsAsync(Guid runId, CancellationToken ct);
}

public sealed record GetOptimizationRunsQuery : IQuery<IReadOnlyList<OptimizationRunDto>>;

public sealed class GetOptimizationRunsQueryHandler(IOptimizationReader reader)
    : IQueryHandler<GetOptimizationRunsQuery, IReadOnlyList<OptimizationRunDto>>
{
    public async Task<Result<IReadOnlyList<OptimizationRunDto>>> HandleAsync(
        GetOptimizationRunsQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListRunsAsync(ct));
}

public sealed record GetRunDecisionsQuery(Guid RunId) : IQuery<IReadOnlyList<OptimizationDecisionDto>>;

public sealed class GetRunDecisionsQueryHandler(IOptimizationReader reader)
    : IQueryHandler<GetRunDecisionsQuery, IReadOnlyList<OptimizationDecisionDto>>
{
    public async Task<Result<IReadOnlyList<OptimizationDecisionDto>>> HandleAsync(
        GetRunDecisionsQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListDecisionsAsync(query.RunId, ct));
}
