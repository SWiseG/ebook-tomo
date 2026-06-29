using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;
using Ebook.Domain.Products;

namespace Ebook.Application.Content;

/// <summary>Um item do checklist de conversão (docs/11, Fase 4) avaliado pela IA.</summary>
public sealed record AuditItemDto(string Item, bool Pass, string Note);

/// <summary>
/// Auditoria de conversão (Fase 7 / docs/13 WS-F): a IA pontua o e-book contra o checklist de
/// persuasão (hook, PAS, prova social, CTA, promessa) e dá um veredito. Gate consultivo antes de publicar.
/// </summary>
public sealed record ConversionAuditDto(string Verdict, int Score, string Summary, IReadOnlyList<AuditItemDto> Items);

public sealed record GetConversionAuditQuery(Guid ProductId) : IQuery<ConversionAuditDto>;

public sealed class GetConversionAuditQueryHandler(
    IProductRepository products,
    IConversionAuditService auditService) : IQueryHandler<GetConversionAuditQuery, ConversionAuditDto>
{
    public async Task<Result<ConversionAuditDto>> HandleAsync(GetConversionAuditQuery query, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(query.ProductId, ct);
        return product is null
            ? Result.Failure<ConversionAuditDto>(ContentErrors.ProductNotFound(query.ProductId))
            : await auditService.AuditAsync(product, ct);
    }
}
