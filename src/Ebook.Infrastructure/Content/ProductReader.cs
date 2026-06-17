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

        var rows = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new
            {
                p.Id, p.Slug, p.Title, p.Status, p.Stage, p.Price, p.Currency, p.PublicationPlatform, p.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return rows.Select(p => new ProductListItemDto(
            p.Id, p.Slug, p.Title, p.Status.ToString(), p.Stage.ToString(),
            p.Price, p.Currency, p.PublicationPlatform?.ToString(), p.CreatedAtUtc)).ToList();
    }

    public async Task<ProductDetailDto?> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null
            ? null
            : new ProductDetailDto(
                p.Id, p.NicheId, p.Slug, p.Title, p.Status.ToString(), p.Stage.ToString(),
                p.QualityTier.ToString(), p.Price, p.Currency, p.LpUrl, p.CheckoutUrl,
                p.KiwifyProductId, p.SalesCopyJson, p.Description, p.EmailLanguage, p.Category,
                p.PublicationPlatform?.ToString(), p.CreatedAtUtc, p.PublishedAtUtc);
    }
}
