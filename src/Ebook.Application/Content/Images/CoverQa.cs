namespace Ebook.Application.Content.Images;

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
}
