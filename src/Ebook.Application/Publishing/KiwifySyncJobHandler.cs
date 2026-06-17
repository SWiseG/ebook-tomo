using Ebook.Application.Common.Jobs;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Publishing;

/// <summary>
/// Rotina de sincronização: confirma, via API pública da Kiwify, que o produto publicado existe na
/// conta (casamento por nome). Achou → <see cref="ProductStatus.Synchronized"/>; não achou →
/// <see cref="ProductStatus.Unsynchronized"/> e retorna falha para o <c>JobWorker</c> reprocessar
/// (3 tentativas → Dead, permanecendo Unsynchronized). Erro de API é transitório: só reprocessa.
/// </summary>
public sealed class KiwifySyncJobHandler(
    IProductRepository products,
    IKiwifyCatalog catalog,
    IUnitOfWork unitOfWork,
    ILogger<KiwifySyncJobHandler> logger) : IJobHandler
{
    public string Type => PublishingJobs.Sync;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        if (!Guid.TryParse(payloadJson, out var productId))
        {
            return Result.Failure(new Error("Publishing.Sync.BadPayload", "Payload de sincronização inválido."));
        }

        var product = await products.GetByIdAsync(productId, ct);
        if (product is null)
        {
            return Result.Failure(PublishingErrors.ProductNotFound(productId));
        }

        if (product.Status is not (ProductStatus.Published or ProductStatus.Synchronized or ProductStatus.Unsynchronized))
        {
            return Result.Success(); // fora da janela de sincronização — no-op idempotente
        }

        var listed = await catalog.ListProductsAsync(ct);
        if (listed.IsFailure)
        {
            return Result.Failure(listed.Error); // erro transitório de API → reprocessa sem mudar status
        }

        var match = listed.Value.FirstOrDefault(
            p => string.Equals(p.Name.Trim(), product.Title.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            product.MarkSynchronized(match.Id);
            await unitOfWork.SaveChangesAsync(ct);
            logger.LogInformation("Produto {Slug} sincronizado com a Kiwify ({KiwifyId})", product.Slug, match.Id);
            return Result.Success();
        }

        // Já confirmado anteriormente: um miss não rebaixa (pode ser eventual/transitório).
        if (product.Status == ProductStatus.Synchronized)
        {
            return Result.Success();
        }

        product.MarkUnsynchronized();
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogWarning("Produto {Slug} não encontrado na Kiwify; marcado Unsynchronized (vai reprocessar)", product.Slug);
        return Result.Failure(PublishingErrors.KiwifyProductNotFound(product.Title));
    }
}
