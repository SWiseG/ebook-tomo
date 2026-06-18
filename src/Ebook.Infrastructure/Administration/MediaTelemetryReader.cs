using Ebook.Application.Administration.Media;
using Ebook.Domain.Abstractions;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Administration;

public sealed class MediaTelemetryReader(EbookDbContext db, IClock clock)
    : IMediaTelemetryReader
{
    public async Task<MediaTelemetryDto> GetTelemetryAsync(CancellationToken ct)
    {
        var today = clock.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // usages agrupados por provedor: hoje e este mês
        var usages = await db.MediaUsages
            .AsNoTracking()
            .Where(u => u.CreatedAtUtc >= monthStart)
            .ToListAsync(ct);

        var providers = usages
            .Where(u => u.Provider != "Cache")
            .GroupBy(u => u.Provider)
            .Select(g =>
            {
                var todayRows = g.Where(u => u.CreatedAtUtc >= today).ToList();
                return new MediaProviderStatDto(
                    Provider: g.Key,
                    GeneratedToday: todayRows.Count(u => !u.CacheHit),
                    GeneratedThisMonth: g.Count(u => !u.CacheHit),
                    CacheHitsToday: 0,  // cache hits ficam no grupo "Cache"
                    DailyLimit: 0,      // não persistido; informativo via config
                    TotalBytesToday: todayRows.Where(u => !u.CacheHit).Sum(u => (long)u.Bytes),
                    AvgDurationMsToday: todayRows.Any(u => !u.CacheHit)
                        ? (int)todayRows.Where(u => !u.CacheHit).Average(u => u.DurationMs)
                        : 0);
            })
            .OrderBy(p => p.Provider)
            .ToList();

        var cacheHitsToday = usages.Count(u => u.CacheHit && u.CreatedAtUtc >= today);
        var cacheEntriesTotal = await db.MediaCache.CountAsync(ct);

        // tamanho total do cache em bytes (soma dos registros que têm bytes gravados)
        var cacheSizeBytes = usages.Where(u => !u.CacheHit).Sum(u => (long)u.Bytes);

        return new MediaTelemetryDto(providers, cacheHitsToday, cacheEntriesTotal, cacheSizeBytes);
    }
}
