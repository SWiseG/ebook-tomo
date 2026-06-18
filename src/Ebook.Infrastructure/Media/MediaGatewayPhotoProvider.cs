using Ebook.Application.Content.Images;
using Ebook.Application.Media;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// Adapta o <see cref="IMediaGateway"/> ao seam existente <see cref="IPhotoProvider"/> (capa/cards/vídeo):
/// monta um brief de FUNDO a partir da palavra-chave do nicho e roda a cadeia free-first. Sem mudar
/// nenhum call-site, os fundos passam a vir de IA generativa grátis (Pollinations) com fallback Pexels →
/// gradiente. O Skia continua sobrepondo título/texto por cima.
/// </summary>
public sealed class MediaGatewayPhotoProvider(IMediaGateway gateway) : IPhotoProvider
{
    public async Task<byte[]?> TryGetBackgroundAsync(string query, CancellationToken ct = default)
    {
        var brief = new MediaBrief(
            Purpose: "background",
            Prompt: $"abstract editorial background for an ebook about {query}, no text, no words, "
                + "soft gradient, subtle, professional, high quality, uncluttered",
            Query: query,
            NicheSlug: query,
            Width: 1024,
            Height: 1536);

        var result = await gateway.GenerateAsync(brief, ct);
        return result.IsSuccess ? result.Value.Bytes : null;
    }
}
