using System.Text.Json;
using Ebook.Application.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Media;

public sealed class PixabayOptions
{
    public const string SectionName = "Pixabay";

    /// <summary>Chave da API Pixabay. Vazia = provedor desligado (gateway pula).</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Banco de fotos/vetores Pixabay (E14, Fase 2): cota grátis generosa, busca por palavras-chave
/// (<see cref="MediaBrief.Query"/>). Gated pela chave. Falha de rede/parse → null (gateway segue).
/// </summary>
public sealed class PixabayMediaResolver(
    HttpClient http,
    IOptions<PixabayOptions> options,
    ILogger<PixabayMediaResolver> logger) : IMediaResolver
{
    public MediaProvider Provider => MediaProvider.Pixabay;
    public bool Enabled => !string.IsNullOrWhiteSpace(options.Value.ApiKey);
    public int DailyLimit => 0;

    public async Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(brief.Query))
        {
            return null;
        }

        try
        {
            var orientation = brief.Height >= brief.Width ? "vertical" : "horizontal";
            var url = $"https://pixabay.com/api/?key={options.Value.ApiKey}&per_page=3&image_type=photo"
                + $"&orientation={orientation}&safesearch=true&q={Uri.EscapeDataString(brief.Query)}";

            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var photoUrl = ExtractUrl(doc);
            return photoUrl is null ? null : await http.GetByteArrayAsync(photoUrl, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Busca Pixabay falhou para '{Query}'; tentando o próximo elo", brief.Query);
            return null;
        }
    }

    // hits[0].largeImageURL
    private static string? ExtractUrl(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("hits", out var hits) && hits.GetArrayLength() > 0
        && hits[0].TryGetProperty("largeImageURL", out var u)
            ? u.GetString()
            : null;
}
