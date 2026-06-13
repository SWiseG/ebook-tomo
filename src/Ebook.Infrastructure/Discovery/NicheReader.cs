using Ebook.Application.Discovery;
using Ebook.Domain.Niches;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Discovery;

public sealed class NicheReader(EbookDbContext db) : INicheReader
{
    public async Task<IReadOnlyList<NicheListItemDto>> ListAsync(string? status, CancellationToken ct)
    {
        var query = db.Niches.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NicheStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(n => n.Status == parsed);
        }

        return await query
            .OrderByDescending(n => n.Score)
            .Select(n => new NicheListItemDto(
                n.Id, n.Slug, n.Name, n.Status.ToString(), n.Score, n.CycleNumber, n.DiscoveredAtUtc))
            .ToListAsync(ct);
    }
}
