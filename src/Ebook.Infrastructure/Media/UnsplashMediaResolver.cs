using System.Net.Http.Headers;
using System.Text.Json;
using Ebook.Application.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Media;

public sealed class UnsplashOptions
{
    public const string SectionName = "Unsplash";

    /// <summary>Access Key da API Unsplash. Vazia = provedor desligado (gateway pula).</summary>
    public string AccessKey { get; set; } = string.Empty;
}

/// <summary>
/// Banco de fotos Unsplash (E14, Fase 2): fotografia editorial de alta qualidade por palavras-chave
/// (<see cref="MediaBrief.Query"/>). Gated pela chave: sem ela, <see cref="Enabled"/> = false e o
/// gateway pula. Qualquer falha de rede/parse → null (gateway tenta o próximo elo).
/// </summary>
public sealed class UnsplashMediaResolver(
    HttpClient http,
    IOptions<UnsplashOptions> options,
    ILogger<UnsplashMediaResolver> logger) : IMediaResolver
{
    public MediaProvider Provider => MediaProvider.Unsplash;
    public bool Enabled => !string.IsNullOrWhiteSpace(options.Value.AccessKey);
    public int DailyLimit => 0; // limite real é por hora (50 demo); o gateway só checa diário

    public async Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(brief.Query))
        {
            return null;
        }

        try
        {
            var orientation = brief.Height >= brief.Width ? "portrait" : "landscape";
            var url = $"https://api.unsplash.com/search/photos?per_page=1&orientation={orientation}&query="
                + Uri.EscapeDataString(brief.Query);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", options.Value.AccessKey);

            using var response = await http.SendAsync(request, ct);
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
            logger.LogWarning(ex, "Busca Unsplash falhou para '{Query}'; tentando o próximo elo", brief.Query);
            return null;
        }
    }

    // results[0].urls.regular
    private static string? ExtractUrl(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0
        && results[0].TryGetProperty("urls", out var urls) && urls.TryGetProperty("regular", out var regular)
            ? regular.GetString()
            : null;
}
