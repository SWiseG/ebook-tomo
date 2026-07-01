namespace Ebook.Domain.Products;

/// <summary>Estatísticas de uma variante de LP para o roteador e para o critério de promoção (C3).</summary>
public sealed record VariantStats(string VariantTag, int Visits, int Conversions, int DaysActive = 0);

/// <summary>Fonte de aleatoriedade injetável (permite seed fixo em testes).</summary>
public interface IRandom
{
    double NextDouble();
}

/// <summary>
/// Roteador de variantes de LP por Thompson Sampling (C2). Função pura — sem dependência de infra.
/// Escolhe a variante com maior probabilidade de conversão, explorando menos as piores variantes
/// e explorando mais as melhores conforme os dados acumulam (multi-armed bandit bayesiano).
/// </summary>
public static class LpVariantRouter
{
    /// <summary>
    /// Seleciona a variante com base no histórico. Sem dados (visits=0) → round-robin uniforme.
    /// Com dados → Thompson Sampling: amostra Beta(conversions+1, non-conversions+1) para cada variante
    /// e retorna o tag da que obteve a maior amostra.
    /// </summary>
    public static string Route(IReadOnlyList<VariantStats> variants, IRandom rng)
    {
        if (variants.Count == 0) throw new ArgumentException("At least one variant required.", nameof(variants));
        if (variants.Count == 1) return variants[0].VariantTag;

        bool hasData = variants.Any(v => v.Visits > 0);
        if (!hasData)
        {
            // Round-robin uniforme quando não há dados
            int idx = (int)(rng.NextDouble() * variants.Count) % variants.Count;
            return variants[idx].VariantTag;
        }

        string best = variants[0].VariantTag;
        double bestSample = -1;
        foreach (var v in variants)
        {
            int alpha = v.Conversions + 1;                     // sucessos + prior
            int beta = Math.Max(v.Visits - v.Conversions, 0) + 1; // falhas + prior
            double sample = SampleBeta(alpha, beta, rng);
            if (sample > bestSample)
            {
                bestSample = sample;
                best = v.VariantTag;
            }
        }
        return best;
    }

    // Beta(a,b) via razão de Gamas: X ~ Gamma(a), Y ~ Gamma(b) → X/(X+Y) ~ Beta(a,b).
    private static double SampleBeta(int alpha, int beta, IRandom rng)
    {
        double ga = SampleGamma(alpha, rng);
        double gb = SampleGamma(beta, rng);
        double total = ga + gb;
        return total < 1e-300 ? 0.5 : ga / total;
    }

    // Gamma(shape) exato para shape < 40; aproximação normal (Wilson-Hilferty) para shape >= 40.
    private static double SampleGamma(int shape, IRandom rng)
    {
        if (shape < 40)
        {
            // Gamma(n) = -sum(ln(Ui)) para n amostras exponenciais Exp(1)
            double sum = 0;
            for (int i = 0; i < shape; i++)
            {
                double u = rng.NextDouble();
                if (u < 1e-300) u = 1e-300;
                sum -= Math.Log(u);
            }
            return sum;
        }

        // Aproximação normal para shape >= 40 (Box-Muller)
        double u1 = rng.NextDouble(), u2 = rng.NextDouble();
        if (u1 < 1e-300) u1 = 1e-300;
        double z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        return Math.Max(shape + z * Math.Sqrt(shape), 1e-10);
    }
}

/// <summary>IRandom com <see cref="System.Random"/> como implementação de produção.</summary>
public sealed class SystemRandom : IRandom
{
    private readonly Random _rng = Random.Shared;
    public double NextDouble() => _rng.NextDouble();
}
