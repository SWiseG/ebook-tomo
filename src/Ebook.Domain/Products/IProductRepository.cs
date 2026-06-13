namespace Ebook.Domain.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);

    void Add(Product product);
}

public interface IArtifactRepository
{
    Task<Artifact?> GetLatestAsync(Guid productId, ArtifactType type, CancellationToken ct = default);

    void Add(Artifact artifact);
}
