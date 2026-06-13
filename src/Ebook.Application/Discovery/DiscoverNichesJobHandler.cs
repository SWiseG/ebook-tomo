using System.Globalization;
using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Discovery;

/// <summary>
/// Motor de descoberta de nichos (E02): coleta sinais das fontes de tendência, agrega por termo,
/// pontua (NicheScorer), ranqueia e cria os top-N nichos Candidate (emitindo NicheDiscovered),
/// guardando as evidências como TrendSnapshot. Idempotente por slug e por ciclo.
/// </summary>
public sealed class DiscoverNichesJobHandler(
    IEnumerable<ITrendSource> sources,
    INicheRepository niches,
    ITrendSnapshotRepository snapshots,
    ISettingsStore settings,
    IFileStore fileStore,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<DiscoverNichesJobHandler> logger) : IJobHandler
{
    private static readonly string[] DefaultCategories =
        ["financas pessoais", "emagrecimento", "produtividade", "desenvolvimento pessoal", "marketing digital"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => DiscoveryJobs.Discover;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<DiscoverNichesJobPayload>(payloadJson, JsonOptions)
            ?? new DiscoverNichesJobPayload();

        var categories = await settings.GetOrDefaultAsync(SettingKeys.DiscoveryCategories, DefaultCategories, ct);
        var topN = payload.TopN ?? await settings.GetOrDefaultAsync(SettingKeys.DiscoveryTopN, 5, ct);
        var weights = await settings.GetOrDefaultAsync(SettingKeys.DiscoveryScoreWeights, ScoreWeights.Default, ct);
        var cycle = (clock.UtcNow.Year - 2026) * 12 + clock.UtcNow.Month;

        var signals = await CollectAsync(categories, ct);
        if (signals.Count == 0)
        {
            logger.LogWarning("Descoberta de nichos: nenhuma fonte retornou sinais neste ciclo {Cycle}", cycle);
            return Result.Success();
        }

        var activeTokens = (await niches.ActiveSlugsAsync(ct))
            .SelectMany(s => s.Split('-', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);

        var ranked = Rank(signals, activeTokens, weights, topN);
        var discovered = await PersistAsync(ranked, cycle, ct);

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Descoberta concluída no ciclo {Cycle}: {Discovered} novos nichos de {Candidates} candidatos",
            cycle, discovered, ranked.Count);
        return Result.Success();
    }

    private async Task<List<SourceSignal>> CollectAsync(string[] categories, CancellationToken ct)
    {
        var collected = new List<SourceSignal>();
        foreach (var category in categories)
        {
            foreach (var source in sources)
            {
                try
                {
                    foreach (var signal in await source.CollectAsync(category, ct))
                    {
                        if (!string.IsNullOrWhiteSpace(signal.Term))
                        {
                            collected.Add(new SourceSignal(source.Source, signal));
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Fonte {Source} falhou para '{Category}'; ignorando", source.Source, category);
                }
            }
        }

        return collected;
    }

    private static List<Candidate> Rank(
        List<SourceSignal> signals, HashSet<string> activeTokens, ScoreWeights weights, int topN)
    {
        return [.. signals
            .GroupBy(x => Slug.From(x.Signal.Term))
            .Where(g => g.Key.Length > 0)
            .Select(g =>
            {
                var history = g.Key.Split('-').Any(activeTokens.Contains) ? 0.75 : 0.45;
                var metrics = new NicheMetrics(
                    g.Average(x => x.Signal.Volume),
                    g.Average(x => x.Signal.Competition),
                    g.Average(x => x.Signal.Monetization),
                    history);
                return new Candidate(
                    g.Key,
                    g.First().Signal.Term,
                    metrics,
                    NicheScorer.Score(metrics, weights),
                    g.GroupBy(x => x.Source).ToDictionary(sg => sg.Key, sg => (IReadOnlyList<TrendSignal>)[.. sg.Select(x => x.Signal)]));
            })
            .OrderByDescending(c => c.Score)
            .Take(Math.Max(1, topN))];
    }

    private async Task<int> PersistAsync(List<Candidate> ranked, int cycle, CancellationToken ct)
    {
        var stamp = clock.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var discovered = 0;

        foreach (var candidate in ranked)
        {
            if (await niches.GetBySlugAsync(candidate.Slug, ct) is not null)
            {
                continue; // já conhecido — idempotente entre ciclos
            }

            var breakdown = JsonSerializer.Serialize(new
            {
                volume = candidate.Metrics.Volume,
                competition = candidate.Metrics.Competition,
                monetization = candidate.Metrics.Monetization,
                history = candidate.Metrics.History,
                sources = candidate.SignalsBySource.Count,
                total = candidate.Score
            }, JsonOptions);

            var niche = Niche.Discover(candidate.Slug, candidate.Term, candidate.Score, breakdown, cycle, clock.UtcNow);
            niches.Add(niche);

            foreach (var (source, signals) in candidate.SignalsBySource)
            {
                var path = $"niches/{candidate.Slug}/trends/{source}-{stamp}.json";
                await fileStore.WriteTextAsync(path, JsonSerializer.Serialize(signals, JsonOptions), ct);
                snapshots.Add(TrendSnapshot.Create(niche.Id, source, path, clock.UtcNow));
            }

            discovered++;
        }

        return discovered;
    }

    private sealed record SourceSignal(TrendSource Source, TrendSignal Signal);

    private sealed record Candidate(
        string Slug,
        string Term,
        NicheMetrics Metrics,
        double Score,
        IReadOnlyDictionary<TrendSource, IReadOnlyList<TrendSignal>> SignalsBySource);
}
