using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;

namespace Ebook.Application.Administration.Dashboard;

public sealed record DashboardSummaryDto(
    int ProductsActive,
    int ProductsInPipeline,
    int NichesCandidate,
    int JobsFailed,
    int JobsPending,
    int AiCallsToday,
    double AiCacheHitRateToday);

/// <summary>Leitura agregada para o painel; implementado na Infrastructure (consulta direta).</summary>
public interface IDashboardReader
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct);
}

public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryDto>;

public sealed class GetDashboardSummaryQueryHandler(IDashboardReader reader)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<Result<DashboardSummaryDto>> HandleAsync(GetDashboardSummaryQuery query, CancellationToken ct) =>
        Result.Success(await reader.GetSummaryAsync(ct));
}
