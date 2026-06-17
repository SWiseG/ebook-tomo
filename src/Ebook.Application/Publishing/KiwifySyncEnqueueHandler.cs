using Ebook.Application.Common.Events;
using Ebook.Application.Common.Jobs;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Publishing;

/// <summary>
/// Ao marcar um produto como publicado, enfileira a rotina de sincronização que confirma o produto
/// na plataforma (Kiwify). Idempotente por <c>sync:{productId}</c>; o retry fica a cargo do JobWorker.
/// </summary>
public sealed class KiwifySyncEnqueueHandler(
    IJobQueue jobQueue,
    ILogger<KiwifySyncEnqueueHandler> logger) : IDomainEventHandler<ProductPublished>
{
    public async Task HandleAsync(ProductPublished domainEvent, CancellationToken ct)
    {
        await jobQueue.EnqueueAsync(new JobRequest(
            PublishingJobs.Sync,
            domainEvent.ProductId.ToString(),
            PublishingJobs.SyncKey(domainEvent.ProductId),
            ProductId: domainEvent.ProductId), ct);

        logger.LogInformation("Produto {ProductId} publicado ({Platform}); sincronização enfileirada",
            domainEvent.ProductId, domainEvent.Platform);
    }
}
