using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Ebook.Domain.Sales;
using Ebook.Domain.Social;
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

    public async Task<IReadOnlyList<Product>> ListByStatusAsync(ProductStatus status, CancellationToken ct = default) =>
        await db.Products.Where(p => p.Status == status).ToListAsync(ct);

    public Task<int> CountByStatusAsync(ProductStatus status, CancellationToken ct = default) =>
        db.Products.CountAsync(p => p.Status == status, ct);

    public void Add(Product product) => db.Products.Add(product);
}

public sealed class SaleRepository(EbookDbContext db) : ISaleRepository
{
    public Task<bool> ExistsAsync(string kiwifyOrderId, SaleType type, CancellationToken ct = default) =>
        db.SaleEvents.AsNoTracking().AnyAsync(s => s.KiwifyOrderId == kiwifyOrderId && s.Type == type, ct);

    public void Add(SaleEvent sale) => db.SaleEvents.Add(sale);
}

public sealed class SocialPostRepository(EbookDbContext db) : ISocialPostRepository
{
    public void Add(SocialPost post) => db.SocialPosts.Add(post);

    public Task<SocialPost?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.SocialPosts.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExistsForProductAsync(Guid productId, CancellationToken ct = default) =>
        db.SocialPosts.AsNoTracking().AnyAsync(p => p.ProductId == productId, ct);

    public async Task<IReadOnlyList<SocialPost>> GetDueAsync(DateTime nowUtc, int take, bool approvedOnly, CancellationToken ct = default) =>
        await db.SocialPosts
            .Where(p => p.Status == SocialPostStatus.Planned && p.ScheduledAtUtc <= nowUtc
                && (!approvedOnly || p.ApprovedAtUtc != null))
            .OrderBy(p => p.ScheduledAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SocialPost>> GetByProductAsync(Guid productId, CancellationToken ct = default) =>
        await db.SocialPosts.AsNoTracking()
            .Where(p => p.ProductId == productId)
            .OrderBy(p => p.Day)
            .ToListAsync(ct);
}

public sealed class ChannelRepository(EbookDbContext db) : IChannelRepository
{
    public void Add(Channel channel) => db.Channels.Add(channel);

    public Task<Channel?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Channels.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Channel?> GetByNicheAsync(Guid nicheId, CancellationToken ct = default) =>
        db.Channels.FirstOrDefaultAsync(c => c.NicheId == nicheId, ct);

    public async Task<IReadOnlyList<Channel>> ListAsync(CancellationToken ct = default) =>
        await db.Channels.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
}

public sealed class KnowledgeRepository(EbookDbContext db) : IKnowledgeRepository
{
    public Task<KnowledgeAsset?> GetPackByNicheAsync(Guid nicheId, CancellationToken ct = default) =>
        db.KnowledgeAssets
            .Where(a => a.NicheId == nicheId && a.Type == KnowledgeAssetType.KnowledgePack)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<KnowledgeAsset?> GetLatestByTypeAsync(Guid nicheId, KnowledgeAssetType type, CancellationToken ct = default) =>
        db.KnowledgeAssets
            .Where(a => a.NicheId == nicheId && a.Type == type)
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

public sealed class LpVariantRepository(EbookDbContext db) : ILpVariantRepository
{
    public void Add(LpVariant variant) => db.LpVariants.Add(variant);

    public async Task<IReadOnlyList<LpVariant>> GetByProductIdAsync(Guid productId, CancellationToken ct = default) =>
        await db.LpVariants.AsNoTracking()
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.VariantTag)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LpVariant>> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await db.LpVariants.AsNoTracking()
            .Where(v => db.Products.Any(p => p.Slug == slug && p.Id == v.ProductId))
            .OrderBy(v => v.VariantTag)
            .ToListAsync(ct);

    public Task<LpVariant?> GetByTagAsync(Guid productId, string tag, CancellationToken ct = default) =>
        db.LpVariants.FirstOrDefaultAsync(v => v.ProductId == productId && v.VariantTag == tag, ct);

    public async Task DeleteByProductIdAsync(Guid productId, CancellationToken ct = default)
    {
        var rows = await db.LpVariants.Where(v => v.ProductId == productId).ToListAsync(ct);
        db.LpVariants.RemoveRange(rows);
    }

    public async Task DeleteOthersAsync(Guid productId, string keepTag, CancellationToken ct = default)
    {
        var rows = await db.LpVariants
            .Where(v => v.ProductId == productId && v.VariantTag != keepTag)
            .ToListAsync(ct);
        db.LpVariants.RemoveRange(rows);
    }
}
