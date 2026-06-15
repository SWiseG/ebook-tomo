namespace Ebook.Domain.Optimization;

/// <summary>Desempenho de um produto na janela do ciclo (vem do funil de MetricDaily).</summary>
public sealed record ProductPerformance(int Visits, int CheckoutClicks, int Sales, decimal Revenue, double Conversion);

/// <summary>Limiares do classificador de ROI (configuráveis via Settings <c>roi.thresholds</c>).</summary>
public sealed record RoiThresholds(
    int MinVisits,
    int KillVisits,
    double ScaleConversion,
    int ScaleSales,
    double IterateConversion)
{
    public static RoiThresholds Default => new(
        MinVisits: 50,
        KillVisits: 200,
        ScaleConversion: 0.03,
        ScaleSales: 5,
        IterateConversion: 0.015);
}

public sealed record RoiVerdict(OptimizationDecisionKind Decision, string Rationale);

/// <summary>
/// Classificador de ROI (E12-01): regra pura e determinística sobre o desempenho do produto.
/// Escalar (vai bem) · Iterar (tem venda mas converte mal) · Matar (tráfego sem venda) · Manter.
/// </summary>
public static class RoiClassifier
{
    public static RoiVerdict Classify(ProductPerformance p, RoiThresholds t)
    {
        if (p.Visits < t.MinVisits)
        {
            return new RoiVerdict(OptimizationDecisionKind.Keep,
                $"Dados insuficientes ({p.Visits} visitas < {t.MinVisits}).");
        }

        if (p.Sales == 0)
        {
            return p.Visits >= t.KillVisits
                ? new RoiVerdict(OptimizationDecisionKind.Kill,
                    $"{p.Visits} visitas e nenhuma venda no ciclo.")
                : new RoiVerdict(OptimizationDecisionKind.Keep,
                    $"Ainda coletando sinal ({p.Visits} visitas, sem venda).");
        }

        if (p.Conversion >= t.ScaleConversion && p.Sales >= t.ScaleSales)
        {
            return new RoiVerdict(OptimizationDecisionKind.Scale,
                $"Conversão {p.Conversion:P1} e {p.Sales} vendas: escalar.");
        }

        if (p.Conversion < t.IterateConversion)
        {
            return new RoiVerdict(OptimizationDecisionKind.Iterate,
                $"Conversão baixa ({p.Conversion:P1}) com {p.Sales} vendas: iterar.");
        }

        return new RoiVerdict(OptimizationDecisionKind.Keep,
            $"Desempenho estável (conversão {p.Conversion:P1}, {p.Sales} vendas).");
    }
}
