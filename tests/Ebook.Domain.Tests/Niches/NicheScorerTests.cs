using Ebook.Domain.Niches;

namespace Ebook.Domain.Tests.Niches;

public class NicheScorerTests
{
    [Fact]
    public void Score_combina_dimensoes_com_pesos()
    {
        var weights = ScoreWeights.Default; // 0.30 / 0.25 / 0.30 / 0.15
        var metrics = new NicheMetrics(Volume: 1, Competition: 0, Monetization: 1, History: 1);

        var score = NicheScorer.Score(metrics, weights);

        Assert.Equal(1.0, score, 3); // tudo no melhor caso → score máximo
    }

    [Fact]
    public void Concorrencia_alta_reduz_o_score()
    {
        var weights = ScoreWeights.Default;
        var baixa = new NicheMetrics(0.8, 0.1, 0.7, 0.5);
        var alta = new NicheMetrics(0.8, 0.9, 0.7, 0.5);

        Assert.True(NicheScorer.Score(baixa, weights) > NicheScorer.Score(alta, weights));
    }

    [Fact]
    public void Score_e_clampeado_entre_0_e_1()
    {
        var weights = ScoreWeights.Default;
        var foraDeFaixa = new NicheMetrics(Volume: 5, Competition: -3, Monetization: 9, History: 2);

        var score = NicheScorer.Score(foraDeFaixa, weights);

        Assert.InRange(score, 0, 1);
    }
}
