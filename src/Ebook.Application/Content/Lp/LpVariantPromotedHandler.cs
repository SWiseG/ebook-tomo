using Ebook.Application.Common.Events;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Products;

namespace Ebook.Application.Content.Lp;

/// <summary>
/// Ao receber <see cref="LpVariantPromoted"/> via Outbox: copia o HTML da variante vencedora
/// para o caminho principal da LP e apaga as demais variantes do banco e do artifact store.
/// Idempotente: se a variante já for a única, não faz nada.
/// </summary>
public sealed class LpVariantPromotedHandler(
    IProductRepository products,
    ILpVariantRepository lpVariants,
    IArtifactStore artifacts) : IDomainEventHandler<LpVariantPromoted>
{
    public async Task HandleAsync(LpVariantPromoted domainEvent, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(domainEvent.ProductId, ct);
        if (product is null) return;

        var winner = await lpVariants.GetByTagAsync(domainEvent.ProductId, domainEvent.WinnerTag, ct);
        if (winner is null) return;

        var winnerBytes = await artifacts.ReadBytesAsync(winner.FilePath, ct);
        if (winnerBytes is null) return;

        // Promove: sobrescreve o bundle principal com o HTML da variante vencedora
        await artifacts.WriteBytesAsync(ContentPaths.LpBundle(product.Slug), winnerBytes, ct);

        // Remove as variantes perdedoras do banco (hard delete)
        await lpVariants.DeleteOthersAsync(domainEvent.ProductId, domainEvent.WinnerTag, ct);
    }
}
