using System.Text.Json;
using Ebook.Application.Optimization;
using Ebook.Domain.Optimization;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Optimization;

public sealed class OptimizationRepository(EbookDbContext db) : IOptimizationRepository
{
    public void AddRun(OptimizationRun run) => db.OptimizationRuns.Add(run);

    public void AddDecision(OptimizationDecision decision) => db.OptimizationDecisions.Add(decision);

    public Task<OptimizationRun?> GetRunByCycleAsync(int cycleNumber, CancellationToken ct = default) =>
        db.OptimizationRuns.FirstOrDefaultAsync(r => r.CycleNumber == cycleNumber, ct);

    public Task<OptimizationDecision?> GetDecisionAsync(Guid id, CancellationToken ct = default) =>
        db.OptimizationDecisions.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<OptimizationDecision>> ListByRunAsync(Guid runId, CancellationToken ct = default) =>
        await db.OptimizationDecisions.Where(d => d.RunId == runId).ToListAsync(ct);
}

public sealed class OptimizationReader(EbookDbContext db) : IOptimizationReader
{
    public async Task<IReadOnlyList<OptimizationRunDto>> ListRunsAsync(CancellationToken ct)
    {
        var runs = await db.OptimizationRuns.AsNoTracking()
            .OrderByDescending(r => r.ExecutedAtUtc)
            .ToListAsync(ct);

        var counts = (await db.OptimizationDecisions.AsNoTracking()
                .GroupBy(d => d.RunId)
                .Select(g => new { RunId = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.RunId, x => x.Count);

        return runs
            .Select(r => new OptimizationRunDto(
                r.Id, r.CycleNumber, r.ExecutedAtUtc, r.Status.ToString(), counts.GetValueOrDefault(r.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<OptimizationDecisionDto>> ListDecisionsAsync(Guid runId, CancellationToken ct)
    {
        var decisions = await db.OptimizationDecisions.AsNoTracking()
            .Where(d => d.RunId == runId)
            .ToListAsync(ct);

        var productIds = decisions.Select(d => d.ProductId).Distinct().ToList();
        var titles = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);

        return decisions
            .Select(d => new OptimizationDecisionDto(
                d.Id, d.ProductId, titles.GetValueOrDefault(d.ProductId, string.Empty),
                d.Decision.ToString(), d.Status.ToString(), Rationale(d.RationaleJson), d.ActionsJson))
            .ToList();
    }

    private static string Rationale(string rationaleJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rationaleJson);
            if (doc.RootElement.TryGetProperty("rationale", out var el) && el.ValueKind == JsonValueKind.String)
            {
                return el.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // usa o JSON cru
        }

        return rationaleJson;
    }
}
