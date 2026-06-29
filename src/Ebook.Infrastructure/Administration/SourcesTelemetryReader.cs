using Ebook.Application.Administration.Sources;
using Ebook.Domain.Abstractions;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Administration;

/// <summary>
/// Telemetria unificada de fontes (Fase 3): agrega <c>AiUsage</c> (texto) e <c>MediaUsage</c> (imagem)
/// por provedor, no mês corrente, com recorte do dia. Espelha o <see cref="MediaTelemetryReader"/>.
/// </summary>
public sealed class SourcesTelemetryReader(EbookDbContext db, IClock clock)
    : ISourcesTelemetryReader
{
    public async Task<SourcesTelemetryDto> GetTelemetryAsync(CancellationToken ct)
    {
        var today = clock.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var aiUsages = await db.AiUsages.AsNoTracking()
            .Where(u => u.CreatedAtUtc >= monthStart).ToListAsync(ct);
        var mediaUsages = await db.MediaUsages.AsNoTracking()
            .Where(u => u.CreatedAtUtc >= monthStart).ToListAsync(ct);

        var sources = new List<SourceStatDto>();

        // Texto (AI Gateway) — exclui cache (Provider "Cache" ou CacheHit), contado à parte
        foreach (var g in aiUsages.Where(u => u.Provider != "Cache" && !u.CacheHit).GroupBy(u => u.Provider))
        {
            var todayRows = g.Where(u => u.CreatedAtUtc >= today).ToList();
            sources.Add(new SourceStatDto(
                Provider: g.Key,
                Kind: "Texto",
                GeneratedToday: todayRows.Count,
                GeneratedThisMonth: g.Count(),
                TokensToday: todayRows.Sum(u => (long)(u.InputTokensEst + u.OutputTokensEst)),
                BytesToday: 0,
                AvgDurationMsToday: todayRows.Count > 0 ? (int)todayRows.Average(u => u.DurationMs) : 0));
        }

        // Imagem (Media Gateway)
        foreach (var g in mediaUsages.Where(u => u.Provider != "Cache" && !u.CacheHit).GroupBy(u => u.Provider))
        {
            var todayRows = g.Where(u => u.CreatedAtUtc >= today).ToList();
            sources.Add(new SourceStatDto(
                Provider: g.Key,
                Kind: "Imagem",
                GeneratedToday: todayRows.Count,
                GeneratedThisMonth: g.Count(),
                TokensToday: 0,
                BytesToday: todayRows.Sum(u => (long)u.Bytes),
                AvgDurationMsToday: todayRows.Count > 0 ? (int)todayRows.Average(u => u.DurationMs) : 0));
        }

        var cacheHitsToday =
            aiUsages.Count(u => u.CacheHit && u.CreatedAtUtc >= today)
            + mediaUsages.Count(u => u.CacheHit && u.CreatedAtUtc >= today);

        var mediaCacheEntries = await db.MediaCache.CountAsync(ct);
        var mediaCacheBytes = mediaUsages.Where(u => !u.CacheHit).Sum(u => (long)u.Bytes);

        var ordered = sources.OrderBy(s => s.Kind).ThenByDescending(s => s.GeneratedThisMonth).ToList();
        return new SourcesTelemetryDto(ordered, cacheHitsToday, mediaCacheEntries, mediaCacheBytes);
    }
}
