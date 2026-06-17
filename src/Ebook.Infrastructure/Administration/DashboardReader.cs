using Ebook.Application.Administration.Dashboard;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Administration;

public sealed class DashboardReader(EbookDbContext db, IClock clock) : IDashboardReader
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var today = clock.UtcNow.Date;

        var productsActive = await db.Products.CountAsync(p => p.Status == ProductStatus.Synchronized, ct);
        var productsInPipeline = await db.Products.CountAsync(p =>
            p.Status == ProductStatus.Pipeline ||
            p.Status == ProductStatus.Reworking ||
            p.Status == ProductStatus.AwaitingApproval ||
            p.Status == ProductStatus.Publishing, ct);
        var nichesCandidate = await db.Niches.CountAsync(n => n.Status == NicheStatus.Candidate, ct);
        var jobsFailed = await db.Jobs.CountAsync(j => j.Status == JobStatus.Dead, ct);
        var jobsPending = await db.Jobs.CountAsync(j => j.Status == JobStatus.Pending, ct);

        var aiToday = await db.AiUsages
            .Where(u => u.CreatedAtUtc >= today)
            .Select(u => u.CacheHit)
            .ToListAsync(ct);

        var hitRate = aiToday.Count == 0 ? 0 : (double)aiToday.Count(h => h) / aiToday.Count;

        // funil dos últimos 30 dias (E11-03) a partir de MetricDaily
        var from = today.AddDays(-30);
        var metrics = await db.MetricDailies.AsNoTracking()
            .Where(m => m.DateUtc >= from)
            .ToListAsync(ct);
        var visits = metrics.Sum(m => m.Visits);
        var clicks = metrics.Sum(m => m.CheckoutClicks);
        var sales = metrics.Sum(m => m.Sales);
        var revenue = metrics.Sum(m => m.Revenue);
        var conversion = visits > 0 ? Math.Round((double)sales / visits, 4) : 0;

        return new DashboardSummaryDto(
            productsActive, productsInPipeline, nichesCandidate,
            jobsFailed, jobsPending, aiToday.Count, Math.Round(hitRate, 3),
            visits, clicks, sales, revenue, conversion);
    }
}
