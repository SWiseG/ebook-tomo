namespace Ebook.Domain.Products;

/// <summary>
/// Critério de promoção automática de variante de LP (C3). Função pura — sem dependência de infra.
/// Retorna o tag da variante vencedora ou null se os critérios de elegibilidade não forem satisfeitos.
/// </summary>
public static class LpPromotion
{
    /// <summary>
    /// Calcula a variante vencedora. Critérios:
    /// - Pelo menos 2 variantes com dados suficientes (volume ≥ minVisits E janela ≥ minDays).
    /// - A candidata vence TODAS as rivais por margem de conversão ≥ 5pp (0.05).
    /// </summary>
    public static string? CalculateWinner(
        IReadOnlyList<VariantStats> stats, int minVisits, int minDays)
    {
        if (stats.Count < 2) return null;

        var eligible = stats
            .Where(s => s.Visits >= minVisits && s.DaysActive >= minDays)
            .ToList();

        if (eligible.Count < 2) return null;

        string? winner = null;
        double bestRate = -1;

        foreach (var candidate in eligible)
        {
            var rate = candidate.Visits > 0 ? (double)candidate.Conversions / candidate.Visits : 0;
            var rivals = eligible.Where(r => r.VariantTag != candidate.VariantTag).ToList();
            bool beatsAll = rivals.All(r =>
            {
                var rivalRate = r.Visits > 0 ? (double)r.Conversions / r.Visits : 0;
                return rate - rivalRate >= 0.05;
            });

            if (beatsAll && rate > bestRate)
            {
                bestRate = rate;
                winner = candidate.VariantTag;
            }
        }

        return winner;
    }
}
