using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;
using Ebook.Domain.Products;

namespace Ebook.Application.Publishing;

/// <summary>Produto como exposto pela API pública (somente leitura) da Kiwify.</summary>
public sealed record KiwifyCatalogProduct(string Id, string Name, string Status, string? CheckoutUrl);

/// <summary>
/// Acesso de leitura ao catálogo da Kiwify via API pública oficial (REST/OAuth). A API NÃO cria
/// produtos — serve para resolver o id e a URL de checkout de um produto já criado no dashboard.
/// </summary>
public interface IKiwifyCatalog
{
    /// <summary>Lista os produtos da conta (resumo; sem URL de checkout).</summary>
    Task<Result<IReadOnlyList<KiwifyCatalogProduct>>> ListProductsAsync(CancellationToken ct);

    /// <summary>Detalhe de um produto, com a URL de checkout resolvida a partir dos links ativos.</summary>
    Task<Result<KiwifyCatalogProduct>> GetProductAsync(string kiwifyProductId, CancellationToken ct);
}

/// <summary>Correspondência do nosso produto na Kiwify: id + URL de checkout para concluir a publicação.</summary>
public sealed record KiwifyProductMatch(string KiwifyProductId, string CheckoutUrl, string Name);

/// <summary>
/// Procura, na Kiwify, o produto correspondente ao nosso (casamento por nome) e devolve o id +
/// URL de checkout para pré-preencher o formulário de "Concluir publicação". Não muda estado.
/// </summary>
public sealed record ResolveKiwifyProductQuery(Guid ProductId) : IQuery<KiwifyProductMatch>;

public sealed class ResolveKiwifyProductQueryHandler(
    IProductRepository products,
    IKiwifyCatalog catalog) : IQueryHandler<ResolveKiwifyProductQuery, KiwifyProductMatch>
{
    public async Task<Result<KiwifyProductMatch>> HandleAsync(ResolveKiwifyProductQuery query, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(query.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<KiwifyProductMatch>(PublishingErrors.ProductNotFound(query.ProductId));
        }

        var listed = await catalog.ListProductsAsync(ct);
        if (listed.IsFailure)
        {
            return Result.Failure<KiwifyProductMatch>(listed.Error);
        }

        var match = listed.Value.FirstOrDefault(
            p => string.Equals(p.Name.Trim(), product.Title.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return Result.Failure<KiwifyProductMatch>(PublishingErrors.KiwifyProductNotFound(product.Title));
        }

        // O resumo da listagem não traz o checkout; o detalhe resolve a URL a partir dos links.
        var detail = await catalog.GetProductAsync(match.Id, ct);
        if (detail.IsFailure)
        {
            return Result.Failure<KiwifyProductMatch>(detail.Error);
        }

        if (string.IsNullOrWhiteSpace(detail.Value.CheckoutUrl))
        {
            return Result.Failure<KiwifyProductMatch>(PublishingErrors.KiwifyCheckoutMissing(product.Title));
        }

        return Result.Success(new KiwifyProductMatch(match.Id, detail.Value.CheckoutUrl!, match.Name));
    }
}
