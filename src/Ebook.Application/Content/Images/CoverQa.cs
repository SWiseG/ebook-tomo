namespace Ebook.Application.Content.Images;

/// <summary>Score detalhado do QA de visão de uma capa (torneio A3).</summary>
public sealed record CoverScore(
    int Score,
    int ThumbnailScore,
    int Contrast,
    bool TitleLegible,
    bool GenreFit,
    IReadOnlyList<string> Issues)
{
    public static CoverScore Failed => new(0, 0, 0, false, false, ["QA indisponível"]);
}

/// <summary>Veredito do QA de visão sobre uma capa gerada por IA (docs/14 WP-8).</summary>
public sealed record CoverQaVerdict(bool Legible, bool TitleMatches, int Score, string Issues)
{
    /// <summary>Aceita só quando o texto é legível E o título bate — senão, cai no Skia rico.</summary>
    public bool Accepted => Legible && TitleMatches;
}

/// <summary>
/// Confere, por VISÃO, se a capa gerada pela IA (caminho full-AI, WP-5) tem o título legível e
/// correto — modelos de imagem costumam embaralhar texto. Reprovou → o chamador usa a composição
/// Skia determinística. Best-effort: indisponível → reprova (fallback seguro).
/// </summary>
public interface ICoverQa
{
    Task<CoverQaVerdict> ReviewAsync(byte[] coverPng, string title, CancellationToken ct = default);

    /// <summary>Score detalhado para o torneio de capas (A3): avalia tamanho cheio + thumbnail ~150px.</summary>
    Task<CoverScore> ScoreAsync(byte[] coverPng, string title, string nicheSlug, CancellationToken ct = default);
}
