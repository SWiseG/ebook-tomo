using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Ebook.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Persistence;

public sealed class NicheRepository(EbookDbContext db) : INicheRepository
{
    public Task<Niche?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Niches.FirstOrDefaultAsync(n => n.Id == id, ct);

    public Task<Niche?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Niches.FirstOrDefaultAsync(n => n.Slug == slug, ct);

    public async Task<IReadOnlyList<string>> ActiveSlugsAsync(CancellationToken ct = default) =>
        await db.Niches.AsNoTracking()
            .Where(n => n.Status == NicheStatus.Active || n.Status == NicheStatus.Selected)
            .Select(n => n.Slug)
            .ToListAsync(ct);

    public void Add(Niche niche) => db.Niches.Add(niche);
}

public sealed class TrendSnapshotRepository(EbookDbContext db) : ITrendSnapshotRepository
{
    public void Add(TrendSnapshot snapshot) => db.TrendSnapshots.Add(snapshot);
}

public sealed class ProductRepository(EbookDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        db.Products.AsNoTracking().AnyAsync(p => p.Slug == slug, ct);

    public Task<Product?> GetByKiwifyProductIdAsync(string kiwifyProductId, CancellationToken ct = default) =>
        db.Products.FirstOrDefaultAsync(p => p.KiwifyProductId == kiwifyProductId, ct);

    public void Add(Product product) => db.Products.Add(product);
}

public sealed class SaleRepository(EbookDbContext db) : ISaleRepository
{
    public Task<bool> ExistsByOrderIdAsync(string kiwifyOrderId, CancellationToken ct = default) =>
        db.SaleEvents.AsNoTracking().AnyAsync(s => s.KiwifyOrderId == kiwifyOrderId, ct);

    public void Add(SaleEvent sale) => db.SaleEvents.Add(sale);
}

public sealed class KnowledgeRepository(EbookDbContext db) : IKnowledgeRepository
{
    public Task<KnowledgeAsset?> GetPackByNicheAsync(Guid nicheId, CancellationToken ct = default) =>
        db.KnowledgeAssets
            .Where(a => a.NicheId == nicheId && a.Type == KnowledgeAssetType.KnowledgePack)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public void Add(KnowledgeAsset asset) => db.KnowledgeAssets.Add(asset);
}

public sealed class ArtifactRepository(EbookDbContext db) : IArtifactRepository
{
    public Task<Artifact?> GetLatestAsync(Guid productId, ArtifactType type, CancellationToken ct = default) =>
        db.Artifacts.AsNoTracking()
            .Where(a => a.ProductId == productId && a.Type == type)
            .OrderByDescending(a => a.Version)
            .FirstOrDefaultAsync(ct);

    public void Add(Artifact artifact) => db.Artifacts.Add(artifact);
}
