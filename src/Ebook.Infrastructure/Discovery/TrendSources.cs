using System.Text.Json;
using Ebook.Application.Discovery;
using Ebook.Domain.Niches;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Discovery;

internal static class TrendHeuristics
{
    private static readonly string[] CommercialWords =
        ["como", "guia", "curso", "passo", "plano", "dieta", "receita", "ganhar", "renda",
         "emagrecer", "aprenda", "metodo", "método", "dicas", "ganhe"];

    /// <summary>Intenção comercial aproximada (0..1) por palavras-chave do termo.</summary>
    public static double Monetization(string term)
    {
        var lower = term.ToLowerInvariant();
        var hits = CommercialWords.Count(w => lower.Contains(w, StringComparison.Ordinal));
        return Math.Clamp(0.4 + hits * 0.15, 0, 1);
    }
}

/// <summary>
/// E02-02: sinal de interesse no Reddit (JSON público de busca). Um sinal por categoria,
/// com volume pelo engajamento e concorrência pelo número de resultados. Degrada para vazio em falha.
/// </summary>
public sealed class RedditTrendSource(IHttpClientFactory httpClientFactory, ILogger<RedditTrendSource> logger) : ITrendSource
{
    public TrendSource Source => TrendSource.Reddit;

    public async Task<IReadOnlyList<TrendSignal>> CollectAsync(string category, CancellationToken ct)
    {
        try
        {
            var http = httpClientFactory.CreateClient("trends");
            var url = $"https://www.reddit.com/search.json?q={Uri.EscapeDataString(category)}&sort=top&t=month&limit=20&raw_json=1";
            var json = await http.GetStringAsync(url, ct);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("children", out var children))
            {
                return [];
            }

            double ups = 0;
            var count = 0;
            foreach (var child in children.EnumerateArray())
            {
                if (child.TryGetProperty("data", out var d) && d.TryGetProperty("ups", out var u))
                {
                    ups += u.GetDouble();
                }

                count++;
            }

            if (count == 0)
            {
                return [];
            }

            return
            [
                new TrendSignal(
                    category,
                    Volume: Math.Min(1.0, ups / 5000.0),
                    Competition: Math.Min(1.0, count / 20.0),
                    Monetization: TrendHeuristics.Monetization(category))
            ];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Reddit indisponível para '{Category}'", category);
            return [];
        }
    }
}

/// <summary>
/// E02-04: long-tails do autocomplete do Google. Cada sugestão vira um candidato; volume cai
/// com a posição. Degrada para vazio em falha.
/// </summary>
public sealed class GoogleAutocompleteTrendSource(
    IHttpClientFactory httpClientFactory, ILogger<GoogleAutocompleteTrendSource> logger) : ITrendSource
{
    private const int MaxSuggestions = 10;

    public TrendSource Source => TrendSource.Autocomplete;

    public async Task<IReadOnlyList<TrendSignal>> CollectAsync(string category, CancellationToken ct)
    {
        try
        {
            var http = httpClientFactory.CreateClient("trends");
            var url = $"https://suggestqueries.google.com/complete/search?client=firefox&hl=pt-BR&q={Uri.EscapeDataString(category)}";
            var json = await http.GetStringAsync(url, ct);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2)
            {
                return [];
            }

            var suggestions = doc.RootElement[1];
            var signals = new List<TrendSignal>();
            var index = 0;
            foreach (var element in suggestions.EnumerateArray())
            {
                var term = element.GetString();
                if (string.IsNullOrWhiteSpace(term))
                {
                    continue;
                }

                signals.Add(new TrendSignal(
                    term,
                    Volume: Math.Max(0.2, 1.0 - index * 0.07),
                    Competition: 0.5,
                    Monetization: TrendHeuristics.Monetization(term)));

                if (++index >= MaxSuggestions)
                {
                    break;
                }
            }

            return signals;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Autocomplete indisponível para '{Category}'", category);
            return [];
        }
    }
}
