namespace Ebook.Application.Content.Images;

public enum ImageTemplate
{
    Cover,
    SocialCard,
    Story
}

/// <summary>Um benefício destacado na capa (caixa com ícone + texto curto) — docs/14 WP-4/6.</summary>
public sealed record CoverFeature(string Text, string Icon = "check");

/// <summary>
/// Conteúdo da capa do e-book. Campos ricos (eyebrow, features, seal, author) são opcionais —
/// preenchidos pelo Diretor de Capa por IA (docs/14 WP-4). Ausentes, a capa degrada para o
/// essencial (título + subtítulo), mantendo compatibilidade.
/// </summary>
public sealed record CoverArt(
    string Title,
    string? Subtitle,
    string? Brand,
    NichePalette Palette,
    string? Eyebrow = null,
    IReadOnlyList<CoverFeature>? Features = null,
    string? Seal = null,
    string? Author = null);

/// <summary>Conteúdo de um card social (feed 1080×1080 ou story 1080×1920).</summary>
public sealed record SocialArt(string Headline, string? Subtext, ImageTemplate Template, NichePalette Palette);

/// <summary>Conteúdo de um carrossel (capa com headline + 1 slide por texto), feed 1080×1080.</summary>
public sealed record CarouselArt(string Headline, string? Brand, IReadOnlyList<string> Slides, NichePalette Palette);

/// <summary>Uma métrica de infográfico: número de impacto + rótulo curto.</summary>
public sealed record InfographicMetric(string Number, string Label);

/// <summary>Conteúdo de um infográfico de métricas (banda com 2–3 números) — docs/13 WS-E.</summary>
public sealed record InfographicArt(IReadOnlyList<InfographicMetric> Metrics, NichePalette Palette);

/// <summary>
/// Composição programática de imagens (E09-01). Implementado na Infrastructure (SkiaSharp).
/// A foto de fundo é opcional; sem ela, usa-se gradiente da paleta.
/// </summary>
public interface IImageComposer
{
    byte[] RenderCover(CoverArt art, byte[]? backgroundPhoto = null);

    /// <summary>Normaliza uma imagem (ex.: capa full-AI, docs/14 WP-5) para o tamanho exato de capa
    /// 2:3 (cover-crop). Garante dimensões consistentes independentemente do que o modelo devolveu.</summary>
    byte[] FitCover(byte[] imageBytes);

    /// <summary>Recorta qualquer imagem para banner 2:1 (1280×640) — ilustrações de capítulo do PDF
    /// ficam grandes e uniformes (sem letterbox de retrato/quadrado). docs/17 P1-5.</summary>
    byte[] FitBanner(byte[] imageBytes);

    /// <summary>Mockup 3D do e-book para marketing, a partir da capa já renderizada.</summary>
    byte[] RenderMockup(byte[] coverPng, NichePalette palette);

    /// <summary>Banner da vitrine Kiwify/Hotmart (~300×250, 1200×1000 hi-res): capa sobre fundo da
    /// paleta. docs/17 P1-7.</summary>
    byte[] RenderMarketplaceBanner(byte[] coverPng, NichePalette palette);

    byte[] RenderSocial(SocialArt art, byte[]? backgroundPhoto = null);

    /// <summary>Renderiza um carrossel: capa (headline) + 1 slide por texto. Todos 1080×1080.</summary>
    IReadOnlyList<byte[]> RenderCarousel(CarouselArt art, byte[]? backgroundPhoto = null);

    /// <summary>Infográfico de métricas (banda horizontal com 2–3 números de impacto). Para o corpo do PDF.</summary>
    byte[] RenderInfographic(InfographicArt art);
}

/// <summary>
/// Busca uma foto de fundo por palavras-chave (E09-02; Pexels/Unsplash com cache local).
/// Degrada graciosamente: retorna null quando não há chave de API ou em qualquer falha.
/// </summary>
public interface IPhotoProvider
{
    Task<byte[]?> TryGetBackgroundAsync(string query, CancellationToken ct = default);
}
