using System.Text.Json;
using Ebook.Application.Common.Events;
using Ebook.Application.Common.Jobs;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Social;

/// <summary>
/// Quando um produto é publicado (E07), enfileira a geração do calendário social (E08).
/// A geração roda na fila (IA + cards) com idempotência por produto.
/// </summary>
public sealed class ProductPublishedHandler(
    IJobQueue jobQueue,
    ILogger<ProductPublishedHandler> logger) : IDomainEventHandler<ProductPublished>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(ProductPublished domainEvent, CancellationToken ct)
    {
        await jobQueue.EnqueueAsync(new JobRequest(
            SocialJobs.Calendar,
            JsonSerializer.Serialize(new CalendarJobPayload(domainEvent.ProductId), JsonOptions),
            SocialJobs.CalendarKey(domainEvent.ProductId),
            ProductId: domainEvent.ProductId), ct);

        logger.LogInformation("Produto {ProductId} publicado: geração de calendário social enfileirada", domainEvent.ProductId);
    }
}
