namespace Ebook.Application.Content.Images;

public enum ImageTemplate
{
    Cover,
    SocialCard,
    Story
}

/// <summary>Conteúdo da capa do e-book.</summary>
public sealed record CoverArt(string Title, string? Subtitle, string? Brand, NichePalette Palette);

/// <summary>Conteúdo de um card social (feed 1080×1080 ou story 1080×1920).</summary>
public sealed record SocialArt(string Headline, string? Subtext, ImageTemplate Template, NichePalette Palette);

/// <summary>
/// Composição programática de imagens (E09-01). Implementado na Infrastructure (SkiaSharp).
/// A foto de fundo é opcional; sem ela, usa-se gradiente da paleta.
/// </summary>
public interface IImageComposer
{
    byte[] RenderCover(CoverArt art, byte[]? backgroundPhoto = null);

    /// <summary>Mockup 3D do e-book para marketing, a partir da capa já renderizada.</summary>
    byte[] RenderMockup(byte[] coverPng, NichePalette palette);

    byte[] RenderSocial(SocialArt art, byte[]? backgroundPhoto = null);
}

/// <summary>
/// Busca uma foto de fundo por palavras-chave (E09-02; Pexels/Unsplash com cache local).
/// Degrada graciosamente: retorna null quando não há chave de API ou em qualquer falha.
/// </summary>
public interface IPhotoProvider
{
    Task<byte[]?> TryGetBackgroundAsync(string query, CancellationToken ct = default);
}
