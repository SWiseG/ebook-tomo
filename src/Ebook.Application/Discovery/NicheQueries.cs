using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;

namespace Ebook.Application.Discovery;

public sealed record NicheListItemDto(
    Guid Id,
    string Slug,
    string Name,
    string Status,
    double Score,
    int CycleNumber,
    DateTime DiscoveredAtUtc);

/// <summary>Leitura projetada de nichos para o painel (consulta direta na Infrastructure).</summary>
public interface INicheReader
{
    Task<IReadOnlyList<NicheListItemDto>> ListAsync(string? status, CancellationToken ct);
}

public sealed record GetNichesQuery(string? Status = null) : IQuery<IReadOnlyList<NicheListItemDto>>;

public sealed class GetNichesQueryHandler(INicheReader reader)
    : IQueryHandler<GetNichesQuery, IReadOnlyList<NicheListItemDto>>
{
    public async Task<Result<IReadOnlyList<NicheListItemDto>>> HandleAsync(GetNichesQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(query.Status, ct));
}
