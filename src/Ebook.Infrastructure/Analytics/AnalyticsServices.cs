using Ebook.Application.Analytics;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Analytics;
using Ebook.Domain.Products;
using Ebook.Domain.Sales;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Analytics;

/// <summary>Grava eventos brutos de tráfego (resolve o produto pelo slug, deriva o canal do UTM).</summary>
public sealed class AnalyticsRecorder(EbookDbContext db, IClock clock) : IAnalyticsRecorder
{
    public async Task RecordAsync(AnalyticsHit hit, CancellationToken ct = default)
    {
        var productId = await db.Products.AsNoTracking()
            .Where(p => p.Slug == hit.Slug)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        db.AnalyticsEvents.Add(AnalyticsEvent.Create(
            productId, hit.Type, ChannelMap.From(hit.UtmSource), clock.UtcNow,
            hit.UtmSource, hit.UtmCampaign, hit.UtmContent, hit.VariantTag));

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Agrega eventos (visitas/cliques) + vendas (SaleEvent) de um dia em MetricDaily.
/// Idempotente: recalcula e sobrescreve os totais por (produto, dia, canal).
/// </summary>
public sealed class MetricsAggregator(EbookDbContext db) : IMetricsAggregator
{
    public async Task<int> AggregateAsync(DateTime dateUtc, CancellationToken ct = default)
    {
        var day = dateUtc.Date;
        var next = day.AddDays(1);

        var rawEvents = await db.AnalyticsEvents.AsNoTracking()
            .Where(e => e.ProductId != null && e.OccurredAtUtc >= day && e.OccurredAtUtc < next)
            .Select(e => new { ProductId = e.ProductId!.Value, e.Channel, e.Type })
            .ToListAsync(ct);

        var events = rawEvents
            .GroupBy(e => (e.ProductId, e.Channel))
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Channel,
                Visits = g.Count(x => x.Type == AnalyticsEventType.Visit),
                Clicks = g.Count(x => x.Type == AnalyticsEventType.CheckoutClick)
            })
            .ToList();

        var rawSales = await db.SaleEvents.AsNoTracking()
            .Where(s => s.ProductId != null && s.OccurredAtUtc >= day && s.OccurredAtUtc < next)
            .Select(s => new { ProductId = s.ProductId!.Value, s.UtmSource, s.NetAmount, s.Type })
            .ToListAsync(ct);

        var sales = rawSales
            .GroupBy(s => (s.ProductId, Channel: ChannelMap.From(s.UtmSource)))
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Channel,
                Sales = g.Count(x => x.Type == SaleType.Sale),
                // receita líquida: vendas menos estornos/chargebacks do dia (atribuídos ao próprio canal)
                Revenue = g.Where(x => x.Type == SaleType.Sale).Sum(x => x.NetAmount)
                          - g.Where(x => x.Type != SaleType.Sale).Sum(x => x.NetAmount)
            })
            .ToList();

        var keys = events.Select(e => (e.ProductId, e.Channel))
            .Union(sales.Select(s => (s.ProductId, s.Channel)))
            .ToList();

        var existing = await db.MetricDailies.Where(m => m.DateUtc == day).ToListAsync(ct);

        foreach (var (productId, channel) in keys)
        {
            var ev = events.FirstOrDefault(e => e.ProductId == productId && e.Channel == channel);
            var sa = sales.FirstOrDefault(s => s.ProductId == productId && s.Channel == channel);

            var row = existing.FirstOrDefault(m => m.ProductId == productId && m.Channel == channel);
            if (row is null)
            {
                row = MetricDaily.Create(productId, day, channel);
                db.MetricDailies.Add(row);
                existing.Add(row);
            }

            row.Set(ev?.Visits ?? 0, ev?.Clicks ?? 0, sa?.Sales ?? 0, sa?.Revenue ?? 0m);
        }

        await db.SaveChangesAsync(ct);
        return keys.Count;
    }
}

/// <summary>Lê o funil agregado (MetricDaily) para o painel.</summary>
public sealed class MetricsReader(EbookDbContext db) : IMetricsReader
{
    public async Task<FunnelDto> GetOverallAsync(DateTime fromUtc, CancellationToken ct)
    {
        var rows = await db.MetricDailies.AsNoTracking()
            .Where(m => m.DateUtc >= fromUtc.Date)
            .ToListAsync(ct);
        return Funnel(rows);
    }

    public async Task<ProductMetricsDto> GetProductAsync(Guid productId, DateTime fromUtc, CancellationToken ct)
    {
        var rows = await db.MetricDailies.AsNoTracking()
            .Where(m => m.ProductId == productId && m.DateUtc >= fromUtc.Date)
            .ToListAsync(ct);

        var byChannel = rows
            .GroupBy(r => r.Channel)
            .Select(g => new ChannelMetricDto(
                g.Key.ToString(),
                g.Sum(x => x.Visits), g.Sum(x => x.CheckoutClicks),
                g.Sum(x => x.Sales), g.Sum(x => x.Revenue)))
            .OrderByDescending(c => c.Visits)
            .ToList();

        return new ProductMetricsDto(Funnel(rows), byChannel);
    }

    public async Task<IReadOnlyList<VariantStats>> GetVariantStatsAsync(Guid productId, int days, CancellationToken ct)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days);
        var raw = await db.AnalyticsEvents.AsNoTracking()
            .Where(e => e.ProductId == productId && e.VariantTag != null && e.OccurredAtUtc >= from)
            .Select(e => new { e.VariantTag, e.Type, e.OccurredAtUtc })
            .ToListAsync(ct);

        return raw
            .GroupBy(e => e.VariantTag!)
            .Select(g => new VariantStats(
                g.Key,
                Visits: g.Count(x => x.Type == AnalyticsEventType.Visit),
                Conversions: g.Count(x => x.Type == AnalyticsEventType.CheckoutClick),
                DaysActive: g.Select(x => x.OccurredAtUtc.Date).Distinct().Count()))
            .OrderBy(v => v.VariantTag)
            .ToList();
    }

    private static FunnelDto Funnel(IReadOnlyCollection<MetricDaily> rows)
    {
        var visits = rows.Sum(r => r.Visits);
        var clicks = rows.Sum(r => r.CheckoutClicks);
        var sales = rows.Sum(r => r.Sales);
        var revenue = rows.Sum(r => r.Revenue);
        var conversion = visits > 0 ? Math.Round((double)sales / visits, 4) : 0;
        return new FunnelDto(visits, clicks, sales, revenue, conversion);
    }
}
