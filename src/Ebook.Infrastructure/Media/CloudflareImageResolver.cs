using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ebook.Application.Media;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// Cloudflare Workers AI (E14-03), Flux/SDXL. Gated por AccountId + ApiToken (`Media:Cloudflare:*`).
/// flux-1-schnell devolve JSON { result: { image: base64 } }; modelos SDXL devolvem PNG binário —
/// trata os dois. Best-effort: falha/parse → null.
/// </summary>
public sealed class CloudflareImageResolver(HttpClient http, IOptions<MediaOptions> options) : IMediaResolver
{
    public MediaProvider Provider => MediaProvider.Cloudflare;

    public bool Enabled => options.Value.Cloudflare.Enabled
        && !string.IsNullOrWhiteSpace(options.Value.Cloudflare.ApiKey)
        && !string.IsNullOrWhiteSpace(options.Value.Cloudflare.AccountId);

    public int DailyLimit => options.Value.Cloudflare.DailyLimit;

    public async Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        var o = options.Value.Cloudflare;
        var model = string.IsNullOrWhiteSpace(o.Model) ? "@cf/black-forest-labs/flux-1-schnell" : o.Model;
        var url = $"https://api.cloudflare.com/client/v4/accounts/{o.AccountId}/ai/run/{model}";

        var payload = JsonSerializer.Serialize(new { prompt = brief.Prompt });
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var binary = await response.Content.ReadAsByteArrayAsync(ct);
            return binary.Length > 256 ? binary : null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("result", out var result)
            && result.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.String)
        {
            try
            {
                return Convert.FromBase64String(image.GetString()!);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return null;
    }
}
