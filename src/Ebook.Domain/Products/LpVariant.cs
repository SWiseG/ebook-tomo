using Ebook.Domain.Common;

namespace Ebook.Domain.Products;

/// <summary>
/// Variante de landing page de um produto (C1). Cada variante tem um tag estável (v1, v2…),
/// um arquivo HTML separado e métricas rastreadas individualmente pelo pixel.
/// </summary>
public sealed class LpVariant : Entity
{
    private LpVariant()
    {
    }

    public Guid ProductId { get; private set; }

    /// <summary>Tag da variante: "v1", "v2", … Imutável após criação.</summary>
    public string VariantTag { get; private set; } = default!;

    /// <summary>Caminho relativo do HTML auto-contido no IArtifactStore.</summary>
    public string FilePath { get; private set; } = default!;

    public DateTime CreatedAt { get; private set; }

    public static LpVariant Create(Guid productId, string variantTag, string filePath, DateTime createdAt) =>
        new()
        {
            ProductId = productId,
            VariantTag = variantTag,
            FilePath = filePath,
            CreatedAt = createdAt,
        };
}

public interface ILpVariantRepository
{
    void Add(LpVariant variant);
    Task<IReadOnlyList<LpVariant>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<LpVariant>> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task DeleteByProductIdAsync(Guid productId, CancellationToken ct = default);
}
