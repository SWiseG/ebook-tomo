using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ebook.Application.Content.Images;
using Ebook.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Content;

public sealed class PexelsOptions
{
    public const string SectionName = "Pexels";

    /// <summary>Chave da API Pexels. Vazia = busca de fotos desligada (usa gradiente da paleta).</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Busca foto de fundo no Pexels por palavras-chave, com cache local no IArtifactStore (E09-02).
/// Degrada graciosamente: sem chave ou em qualquer falha de rede/parse, retorna null.
/// </summary>
public sealed class PexelsPhotoProvider(
    HttpClient http,
    IArtifactStore artifactStore,
    IOptions<PexelsOptions> options,
    ILogger<PexelsPhotoProvider> logger) : IPhotoProvider
{
    public async Task<byte[]?> TryGetBackgroundAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            return null;
        }

        var cachePath = $"cache/photos/{Hash(query)}.jpg";
        var cached = await artifactStore.ReadBytesAsync(cachePath, ct);
        if (cached is not null)
        {
            return cached;
        }

        try
        {
            var url = "https://api.pexels.com/v1/search?per_page=1&orientation=portrait&query="
                + Uri.EscapeDataString(query);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(options.Value.ApiKey);

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var photoUrl = ExtractFirstPhotoUrl(await JsonDocument.ParseAsync(stream, cancellationToken: ct));
            if (photoUrl is null)
            {
                return null;
            }

            var bytes = await http.GetByteArrayAsync(photoUrl, ct);
            await artifactStore.WriteBytesAsync(cachePath, bytes, ct);
            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Busca de foto no Pexels falhou para '{Query}'; seguindo com gradiente", query);
            return null;
        }
    }

    private static string? ExtractFirstPhotoUrl(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("photos", out var photos) || photos.GetArrayLength() == 0)
        {
            return null;
        }

        return photos[0].TryGetProperty("src", out var src) && src.TryGetProperty("large", out var large)
            ? large.GetString()
            : null;
    }

    private static string Hash(string query) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(query.ToLowerInvariant())))[..16];
}
