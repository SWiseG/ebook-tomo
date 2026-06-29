using Ebook.Application.Content.Images;
using Ebook.Application.Knowledge;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

public sealed class FakeStyleAnalyzer : IStyleAnalyzer
{
    public Task<Result<string>> AnalyzeAsync(byte[] imageBytes, string nicheName, CancellationToken ct = default)
    {
        var json = """
            {
              "summary": "Estilo minimalista com cores vibrantes",
              "palette": "#1A1A2E, #E94560, #F5F5F5",
              "typography": "Sans-serif bold para títulos",
              "composition": "Centralizado com muito espaço negativo",
              "visualHook": "Contraste forte entre fundo escuro e texto claro",
              "promptHints": ["dark background", "bold typography", "high contrast"]
            }
            """;
        return Task.FromResult(Result.Success(json));
    }
}

/// <summary>QA de capa fake: reprova ReviewAsync e retorna score zero em ScoreAsync (fallback Skia nos testes).</summary>
public sealed class FakeCoverQa : ICoverQa
{
    public Task<CoverQaVerdict> ReviewAsync(byte[] coverPng, string title, CancellationToken ct = default) =>
        Task.FromResult(new CoverQaVerdict(Legible: false, TitleMatches: false, Score: 0, Issues: "fake"));

    public Task<CoverScore> ScoreAsync(byte[] coverPng, string title, string nicheSlug, CancellationToken ct = default) =>
        Task.FromResult(CoverScore.Failed);
}

/// <summary>
/// QA de capa fake com scores configuráveis por chamada: para testar o torneio (A3).
/// Cada chamada a ScoreAsync consome um score da fila; se esgotada, retorna zero.
/// ReviewAsync sempre aprova (comportamento que permite testar tournamentSize=1 com full-AI ativo).
/// </summary>
public sealed class ScoringFakeCoverQa : ICoverQa
{
    private int _scoreCalls;
    private readonly int[] _scores;

    public int ScoreCalls => _scoreCalls;

    public ScoringFakeCoverQa(params int[] scores) => _scores = scores;

    public Task<CoverQaVerdict> ReviewAsync(byte[] coverPng, string title, CancellationToken ct = default) =>
        Task.FromResult(new CoverQaVerdict(Legible: true, TitleMatches: true, Score: 75, Issues: string.Empty));

    public Task<CoverScore> ScoreAsync(byte[] coverPng, string title, string nicheSlug, CancellationToken ct = default)
    {
        var idx = Interlocked.Increment(ref _scoreCalls) - 1;
        var score = idx < _scores.Length ? _scores[idx] : 0;
        return Task.FromResult(new CoverScore(score, score, score, true, true, []));
    }
}
