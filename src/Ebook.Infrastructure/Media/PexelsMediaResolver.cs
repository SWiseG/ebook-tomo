using Ebook.Application.Media;
using Ebook.Infrastructure.Content;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// Último elo da cadeia (E14): banco de fotos Pexels (reusa o <see cref="PexelsPhotoProvider"/>).
/// Sem chave Pexels, retorna null (gateway falha → o composer cai no gradiente da paleta).
/// Usa a busca por palavras-chave (<see cref="MediaBrief.Query"/>), não o prompt generativo.
/// </summary>
public sealed class PexelsMediaResolver(PexelsPhotoProvider pexels) : IMediaResolver
{
    public MediaProvider Provider => MediaProvider.Pexels;
    public bool Enabled => true; // o provider já retorna null sem chave configurada
    public int DailyLimit => 0;

    public Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct) =>
        pexels.TryGetBackgroundAsync(brief.Query, ct);
}
