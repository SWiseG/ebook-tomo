namespace Ebook.Domain.Niches;

/// <summary>Métricas normalizadas (0..1) de um candidato a nicho, agregadas das fontes de tendência.</summary>
public sealed record NicheMetrics(double Volume, double Competition, double Monetization, double History);

/// <summary>
/// Pesos do score de nicho (E02-05). Somam ~1. Ajustáveis via Settings e, no futuro,
/// pelo feedback do ROI Optimizer (E02-08).
/// </summary>
public sealed record ScoreWeights(double Volume, double Competition, double Monetization, double History)
{
    public static ScoreWeights Default => new(0.30, 0.25, 0.30, 0.15);
}

/// <summary>
/// Motor de score de nicho: combina volume, baixa concorrência, monetização e afinidade
/// histórica num único valor 0..1. Função pura — determinística e testável.
/// </summary>
public static class NicheScorer
{
    public static double Score(NicheMetrics metrics, ScoreWeights weights)
    {
        var volume = Clamp01(metrics.Volume) * weights.Volume;
        var competition = (1 - Clamp01(metrics.Competition)) * weights.Competition; // menos concorrência = melhor
        var monetization = Clamp01(metrics.Monetization) * weights.Monetization;
        var history = Clamp01(metrics.History) * weights.History;
        return Math.Round(volume + competition + monetization + history, 4);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}
