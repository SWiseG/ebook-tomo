using Ebook.Domain.Products;

namespace Ebook.Domain.Tests.Products;

public class LpVariantRouterTests
{
    private sealed class SeededRandom(int seed) : IRandom
    {
        private readonly Random _r = new(seed);
        public double NextDouble() => _r.NextDouble();
    }

    [Fact]
    public void Variante_com_maior_conversao_vence_maioria_em_mil_rounds()
    {
        // v1: 5% conversão; v2: 50% → Thompson Sampling deve eleger v2 na vasta maioria
        var stats = new List<VariantStats>
        {
            new("v1", Visits: 40, Conversions: 2),
            new("v2", Visits: 40, Conversions: 20),
        };
        var rng = new SeededRandom(42);

        int v2Wins = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (LpVariantRouter.Route(stats, rng) == "v2") v2Wins++;
        }

        Assert.True(v2Wins > 700, $"v2 deveria vencer >70% dos rounds; venceu {v2Wins}/1000");
    }

    [Fact]
    public void Sem_dados_distribui_aproximadamente_uniforme()
    {
        var stats = new List<VariantStats>
        {
            new("v1", 0, 0),
            new("v2", 0, 0),
        };
        var rng = new SeededRandom(99);

        var counts = new Dictionary<string, int> { ["v1"] = 0, ["v2"] = 0 };
        for (int i = 0; i < 1000; i++)
            counts[LpVariantRouter.Route(stats, rng)]++;

        // distribuição uniforme com tolerância de ±10%
        Assert.InRange(counts["v1"], 400, 600);
        Assert.InRange(counts["v2"], 400, 600);
    }

    [Fact]
    public void Uma_so_variante_sempre_e_retornada()
    {
        var stats = new List<VariantStats> { new("v1", 5, 2) };
        var rng = new SeededRandom(1);

        for (int i = 0; i < 10; i++)
            Assert.Equal("v1", LpVariantRouter.Route(stats, rng));
    }

    [Fact]
    public void Lista_vazia_lanca_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => LpVariantRouter.Route([], new SeededRandom(1)));
    }

    [Fact]
    public void Tres_variantes_com_dados_as_melhores_vencem()
    {
        // v3 tem melhor taxa (~60%) — deve vencer a maioria
        var stats = new List<VariantStats>
        {
            new("v1", 20, 2),  // 10%
            new("v2", 20, 6),  // 30%
            new("v3", 20, 12), // 60%
        };
        var rng = new SeededRandom(7);

        var counts = new Dictionary<string, int> { ["v1"] = 0, ["v2"] = 0, ["v3"] = 0 };
        for (int i = 0; i < 1000; i++)
            counts[LpVariantRouter.Route(stats, rng)]++;

        Assert.True(counts["v3"] > counts["v1"], $"v3 ({counts["v3"]}) deveria superar v1 ({counts["v1"]})");
        Assert.True(counts["v3"] > counts["v2"], $"v3 ({counts["v3"]}) deveria superar v2 ({counts["v2"]})");
    }
}
