using Ebook.Application.Ai;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
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
    IFileStore fileStore,
    IAiGateway aiGateway) : IQueryHandler<GetConversionAuditQuery, ConversionAuditDto>
{
    private const int MaxManuscriptChars = 16000; // limita tokens; o hook/estrutura aparecem cedo

    public async Task<Result<ConversionAuditDto>> HandleAsync(GetConversionAuditQuery query, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(query.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<ConversionAuditDto>(ContentErrors.ProductNotFound(query.ProductId));
        }

        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1), ct);
        if (manuscript is null)
        {
            return Result.Failure<ConversionAuditDto>(ContentErrors.ManuscriptMissing(product.Slug));
        }

        var headline = product.Title;
        var salesCopy = await fileStore.ReadTextAsync(ContentPaths.SalesCopy(product.Slug), ct);
        if (salesCopy is not null)
        {
            var parsedCopy = AiJson.Parse<SalesCopyDto>(salesCopy, "ebook.sales-copy");
            if (parsedCopy.IsSuccess && !string.IsNullOrWhiteSpace(parsedCopy.Value.Headline))
            {
                headline = parsedCopy.Value.Headline;
            }
        }

        var trimmed = manuscript.Length > MaxManuscriptChars ? manuscript[..MaxManuscriptChars] : manuscript;

        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "ebook.audit",
            PromptTemplate: "ebook/audit",
            Variables: new Dictionary<string, string>
            {
                ["headline"] = headline,
                ["manuscript"] = trimmed,
            },
            ProductId: product.Id), ct);

        return ai.IsFailure
            ? Result.Failure<ConversionAuditDto>(ai.Error)
            : AiJson.Parse<ConversionAuditDto>(ai.Value.Content, "ebook.audit");
    }
}
