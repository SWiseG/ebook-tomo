using Ebook.Application.Common.Events;
using Ebook.Domain.Products;

namespace Ebook.Application.Common.Realtime;

/// <summary>
/// Reemite as transições de domínio de um produto como notificações em tempo real.
/// Roda no fluxo do Outbox (at-least-once, idempotente por EventId+Handler); o
/// <see cref="IRealtimeNotifier"/> é best-effort, então nunca bloqueia o despacho.
/// O painel reage refazendo o fetch do produto/lista.
/// </summary>
public sealed class ProductRealtimeHandler(IRealtimeNotifier notifier) :
    IDomainEventHandler<ProductCreated>,
    IDomainEventHandler<ProductStageAdvanced>,
    IDomainEventHandler<ProductSubmittedForApproval>,
    IDomainEventHandler<ProductRejected>,
    IDomainEventHandler<ProductPublishingStarted>,
    IDomainEventHandler<ProductPublished>,
    IDomainEventHandler<ProductSynchronized>,
    IDomainEventHandler<ProductUnsynchronized>,
    IDomainEventHandler<ProductRetired>
{
    public Task HandleAsync(ProductCreated e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductCreated)), ct);

    public Task HandleAsync(ProductStageAdvanced e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductStageAdvanced)), ct);

    public Task HandleAsync(ProductSubmittedForApproval e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductSubmittedForApproval)), ct);

    public Task HandleAsync(ProductRejected e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductRejected)), ct);

    public Task HandleAsync(ProductPublishingStarted e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductPublishingStarted)), ct);

    public Task HandleAsync(ProductPublished e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductPublished)), ct);

    public Task HandleAsync(ProductSynchronized e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductSynchronized)), ct);

    public Task HandleAsync(ProductUnsynchronized e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductUnsynchronized)), ct);

    public Task HandleAsync(ProductRetired e, CancellationToken ct) =>
        notifier.ProductChangedAsync(new RealtimeProductChanged(e.ProductId, nameof(ProductRetired)), ct);
}
