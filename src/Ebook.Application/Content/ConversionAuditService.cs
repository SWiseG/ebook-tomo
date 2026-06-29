using Ebook.Application.Ai;
using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;

namespace Ebook.Application.Content;

public interface IConversionAuditService
{
    Task<Result<ConversionAuditDto>> AuditAsync(Product product, CancellationToken ct);
}

public sealed class ConversionAuditService(
    IFileStore fileStore,
    IAiGateway aiGateway) : IConversionAuditService
{
    private const int MaxManuscriptChars = 16000;

    public async Task<Result<ConversionAuditDto>> AuditAsync(Product product, CancellationToken ct)
    {
        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1), ct);
        if (manuscript is null)
            return Result.Failure<ConversionAuditDto>(ContentErrors.ManuscriptMissing(product.Slug));

        var headline = product.Title;
        var salesCopy = await fileStore.ReadTextAsync(ContentPaths.SalesCopy(product.Slug), ct);
        if (salesCopy is not null)
        {
            var parsedCopy = AiJson.Parse<SalesCopyDto>(salesCopy, "ebook.sales-copy");
            if (parsedCopy.IsSuccess && !string.IsNullOrWhiteSpace(parsedCopy.Value.Headline))
                headline = parsedCopy.Value.Headline;
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
