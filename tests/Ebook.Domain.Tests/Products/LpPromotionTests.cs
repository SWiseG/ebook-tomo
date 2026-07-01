using Ebook.Domain.Products;

namespace Ebook.Domain.Tests.Products;

public class LpPromotionTests
{
    [Fact]
    public void Vencedora_com_volume_e_dias_suficientes_e_margem_retorna_tag()
    {
        var stats = new List<VariantStats>
        {
            new("v1", Visits: 120, Conversions: 6, DaysActive: 10),  // 5%
            new("v2", Visits: 120, Conversions: 18, DaysActive: 10), // 15%
        };

        var winner = LpPromotion.CalculateWinner(stats, minVisits: 100, minDays: 7);

        Assert.Equal("v2", winner); // 15% - 5% = 10pp ≥ 5pp
    }

    [Fact]
    public void Volume_insuficiente_retorna_null()
    {
        var stats = new List<VariantStats>
        {
            new("v1", Visits: 50, Conversions: 3, DaysActive: 10),
            new("v2", Visits: 50, Conversions: 8, DaysActive: 10),
        };

        var winner = LpPromotion.CalculateWinner(stats, minVisits: 100, minDays: 7);

        Assert.Null(winner);
    }

    [Fact]
    public void Janela_insuficiente_retorna_null()
    {
        var stats = new List<VariantStats>
        {
            new("v1", Visits: 200, Conversions: 10, DaysActive: 3),
            new("v2", Visits: 200, Conversions: 30, DaysActive: 3),
        };

        var winner = LpPromotion.CalculateWinner(stats, minVisits: 100, minDays: 7);

        Assert.Null(winner);
    }

    [Fact]
    public void Margem_insuficiente_retorna_null()
    {
        // v2 tem apenas 3pp de vantagem, menos do que os 5pp exigidos
        var stats = new List<VariantStats>
        {
            new("v1", Visits: 200, Conversions: 20, DaysActive: 10), // 10%
            new("v2", Visits: 200, Conversions: 26, DaysActive: 10), // 13%
        };

        var winner = LpPromotion.CalculateWinner(stats, minVisits: 100, minDays: 7);

        Assert.Null(winner);
    }

    [Fact]
    public void Apenas_uma_variante_retorna_null()
    {
        var stats = new List<VariantStats>
        {
            new("v1", Visits: 500, Conversions: 50, DaysActive: 14),
        };

        var winner = LpPromotion.CalculateWinner(stats, minVisits: 100, minDays: 7);

        Assert.Null(winner);
    }
}
