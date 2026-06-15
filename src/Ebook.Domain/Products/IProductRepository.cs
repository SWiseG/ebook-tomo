namespace Ebook.Domain.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);

    /// <summary>Resolve o produto a partir do id externo da Kiwify (mapeia webhooks de venda).</summary>
    Task<Product?> GetByKiwifyProductIdAsync(string kiwifyProductId, CancellationToken ct = default);

    /// <summary>Carrega os agregados de produtos em um status (ex.: Live, para o ciclo de otimização).</summary>
    Task<IReadOnlyList<Product>> ListByStatusAsync(ProductStatus status, CancellationToken ct = default);

    Task<int> CountByStatusAsync(ProductStatus status, CancellationToken ct = default);

    void Add(Product product);
}

public interface IArtifactRepository
{
    Task<Artifact?> GetLatestAsync(Guid productId, ArtifactType type, CancellationToken ct = default);

    void Add(Artifact artifact);
}
