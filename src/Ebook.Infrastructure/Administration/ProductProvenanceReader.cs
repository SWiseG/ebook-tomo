using Ebook.Application.Administration.Provenance;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Administration;

/// <summary>
/// Proveniência do produto (Fase 3B): lista as gerações de TEXTO (<c>AiUsage</c>) e de IMAGEM
/// (<c>MediaUsage</c>) atribuídas ao produto, em ordem cronológica. Mostra quem fez o quê no PDF.
/// </summary>
public sealed class ProductProvenanceReader(EbookDbContext db) : IProductProvenanceReader
{
    public async Task<ProductProvenanceDto> GetAsync(Guid productId, CancellationToken ct)
    {
        var ai = await db.AiUsages.AsNoTracking()
            .Where(u => u.ProductId == productId)
            .OrderBy(u => u.CreatedAtUtc).ToListAsync(ct);

        var media = await db.MediaUsages.AsNoTracking()
            .Where(u => u.ProductId == productId)
            .OrderBy(u => u.CreatedAtUtc).ToListAsync(ct);

        var text = ai.Select(u => new ProvenanceEntryDto(
            u.Purpose, u.Provider, u.CacheHit, u.InputTokensEst + u.OutputTokensEst, 0, u.CreatedAtUtc)).ToList();

        var images = media.Select(u => new ProvenanceEntryDto(
            u.Purpose, u.Provider, u.CacheHit, 0, u.Bytes, u.CreatedAtUtc)).ToList();

        return new ProductProvenanceDto(
            text,
            images,
            text.Count,
            images.Count,
            text.Sum(e => e.Tokens),
            images.Sum(e => e.Bytes));
    }
}
