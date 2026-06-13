using Ebook.Application.Content;
using Ebook.Domain.Products;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Content;

public sealed class ProductReader(EbookDbContext db) : IProductReader
{
    public async Task<IReadOnlyList<ProductListItemDto>> ListAsync(string? status, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProductStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(p => p.Status == parsed);
        }

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProductListItemDto(
                p.Id, p.Slug, p.Title, p.Status.ToString(), p.Stage.ToString(),
                p.Price, p.Currency, p.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<ProductDetailDto?> GetDetailAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetailDto(
                p.Id, p.NicheId, p.Slug, p.Title, p.Status.ToString(), p.Stage.ToString(),
                p.QualityTier.ToString(), p.Price, p.Currency, p.LpUrl, p.CheckoutUrl,
                p.KiwifyProductId, p.SalesCopyJson, p.CreatedAtUtc, p.PublishedAtUtc))
            .FirstOrDefaultAsync(ct);
}
