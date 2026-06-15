using Ebook.Domain.Optimization;

namespace Ebook.Domain.Tests.Optimization;

public class RoiClassifierTests
{
    private static readonly RoiThresholds T = RoiThresholds.Default;

    private static ProductPerformance Perf(int visits, int sales, double conversion) =>
        new(visits, 0, sales, 0m, conversion);

    [Fact]
    public void Poucas_visitas_mantem()
    {
        var v = RoiClassifier.Classify(Perf(visits: 10, sales: 0, conversion: 0), T);
        Assert.Equal(OptimizationDecisionKind.Keep, v.Decision);
    }

    [Fact]
    public void Trafego_sem_venda_mata()
    {
        var v = RoiClassifier.Classify(Perf(visits: 300, sales: 0, conversion: 0), T);
        Assert.Equal(OptimizationDecisionKind.Kill, v.Decision);
    }

    [Fact]
    public void Trafego_baixo_sem_venda_ainda_mantem()
    {
        var v = RoiClassifier.Classify(Perf(visits: 100, sales: 0, conversion: 0), T);
        Assert.Equal(OptimizationDecisionKind.Keep, v.Decision); // abaixo de KillVisits
    }

    [Fact]
    public void Alta_conversao_e_vendas_escala()
    {
        var v = RoiClassifier.Classify(Perf(visits: 300, sales: 15, conversion: 0.05), T);
        Assert.Equal(OptimizationDecisionKind.Scale, v.Decision);
    }

    [Fact]
    public void Conversao_baixa_com_vendas_itera()
    {
        var v = RoiClassifier.Classify(Perf(visits: 300, sales: 2, conversion: 0.0067), T);
        Assert.Equal(OptimizationDecisionKind.Iterate, v.Decision);
    }

    [Fact]
    public void Desempenho_mediano_mantem()
    {
        var v = RoiClassifier.Classify(Perf(visits: 300, sales: 6, conversion: 0.02), T);
        Assert.Equal(OptimizationDecisionKind.Keep, v.Decision); // converte ok mas não o bastante p/ escalar
    }
}
