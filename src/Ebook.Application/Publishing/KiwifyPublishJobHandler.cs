using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Content;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Publishing;

/// <summary>
/// Cria o produto na Kiwify via <see cref="IKiwifyPublisher"/> e, em caso de sucesso,
/// leva o produto a Live (grava id Kiwify + URL de checkout, que o redirect /go/{slug} consome).
/// Re-entrante: no-op quando o produto não está em Publishing ou já tem id Kiwify.
/// </summary>
public sealed class KiwifyPublishJobHandler(
    IProductRepository products,
    IKiwifyPublisher publisher,
    IClock clock,
    IUnitOfWork unitOfWork,
    ILogger<KiwifyPublishJobHandler> logger) : IJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => PublishingJobs.Publish;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<PublishJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(PublishingErrors.ProductNotFound(payload.ProductId));
        }

        if (product.Status != ProductStatus.Publishing || !string.IsNullOrEmpty(product.KiwifyProductId))
        {
            return Result.Success(); // já publicado ou fora da janela de publicação
        }

        var request = new KiwifyPublishRequest(
            product.Id,
            product.Slug,
            product.Title,
            Description(product),
            product.Price,
            product.Currency,
            ContentPaths.Pdf(product.Slug, 1),
            product.LpUrl);

        var outcome = await publisher.PublishAsync(request, ct);
        if (outcome.IsFailure)
        {
            return Result.Failure(outcome.Error);
        }

        var marked = product.MarkPublished(
            outcome.Value.KiwifyProductId, outcome.Value.CheckoutUrl, product.LpUrl ?? string.Empty, clock.UtcNow);
        if (marked.IsFailure)
        {
            return Result.Failure(marked.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Produto {Slug} publicado na Kiwify ({KiwifyId})",
            product.Slug, outcome.Value.KiwifyProductId);
        return Result.Success();
    }

    private static string Description(Product product)
    {
        try
        {
            using var doc = JsonDocument.Parse(product.SalesCopyJson);
            if (doc.RootElement.TryGetProperty("headline", out var headline)
                && headline.ValueKind == JsonValueKind.String)
            {
                return headline.GetString() ?? product.Title;
            }
        }
        catch (JsonException)
        {
            // copy ausente/inválida → usa o título
        }

        return product.Title;
    }
}
