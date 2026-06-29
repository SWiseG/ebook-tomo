using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;

namespace Ebook.Application.Administration.Provenance;

/// <summary>Uma geração registrada para o produto (texto ou imagem) — docs/13 §3 (proveniência).</summary>
public sealed record ProvenanceEntryDto(
    string Purpose,
    string Provider,
    bool CacheHit,
    long Tokens,        // texto (0 para imagem)
    long Bytes,         // imagem (0 para texto)
    DateTime AtUtc);

/// <summary>De onde o PDF do produto veio: quem fez o texto (IA) e quem fez as imagens (Media Gateway).</summary>
public sealed record ProductProvenanceDto(
    IReadOnlyList<ProvenanceEntryDto> Text,
    IReadOnlyList<ProvenanceEntryDto> Images,
    int TextCount,
    int ImageCount,
    long TotalTokens,
    long TotalBytes);

public interface IProductProvenanceReader
{
    Task<ProductProvenanceDto> GetAsync(Guid productId, CancellationToken ct);
}

public sealed record GetProductProvenanceQuery(Guid ProductId) : IQuery<ProductProvenanceDto>;

public sealed class GetProductProvenanceQueryHandler(IProductProvenanceReader reader)
    : IQueryHandler<GetProductProvenanceQuery, ProductProvenanceDto>
{
    public async Task<Result<ProductProvenanceDto>> HandleAsync(GetProductProvenanceQuery query, CancellationToken ct) =>
        Result.Success(await reader.GetAsync(query.ProductId, ct));
}
