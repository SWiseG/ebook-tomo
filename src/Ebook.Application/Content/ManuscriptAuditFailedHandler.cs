using System.Text.Json;
using Ebook.Application.Common.Events;
using Ebook.Application.Common.Jobs;
using Ebook.Domain.Products;

namespace Ebook.Application.Content;

/// <summary>
/// Recebe <see cref="ManuscriptAuditFailed"/> via Outbox e re-enfileira o job de revisão
/// com <c>RetryAttempt > 0</c>, forçando a regeneração do manuscrito + continuidade.
/// Idempotente: a chave de idempotência inclui o número da tentativa.
/// </summary>
public sealed class ManuscriptAuditFailedHandler(
    IProductRepository products,
    IJobQueue jobQueue) : IDomainEventHandler<ManuscriptAuditFailed>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(ManuscriptAuditFailed domainEvent, CancellationToken ct)
    {
        // Confirma que o produto ainda existe antes de re-enfileirar
        var product = await products.GetByIdAsync(domainEvent.ProductId, ct);
        if (product is null) return;

        await jobQueue.EnqueueAsync(new JobRequest(
            ContentJobs.Review,
            JsonSerializer.Serialize(
                new ReviewJobPayload(domainEvent.ProductId, domainEvent.Attempt), JsonOptions),
            ContentJobs.ReviewRetryKey(domainEvent.ProductId, domainEvent.Attempt),
            ProductId: domainEvent.ProductId), ct);
    }
}
