using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ebook.Application.Media;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// HuggingFace Inference API (E14-04), SDXL/Flux. Gated por token (`Media:HuggingFace:ApiKey`).
/// Devolve a imagem binária; 503 = modelo carregando (vira null → próximo provedor). Best-effort.
/// </summary>
public sealed class HuggingFaceImageResolver(HttpClient http, IOptions<MediaOptions> options) : IMediaResolver
{
    public MediaProvider Provider => MediaProvider.HuggingFace;
    public bool Enabled => options.Value.HuggingFace.Enabled && !string.IsNullOrWhiteSpace(options.Value.HuggingFace.ApiKey);
    public int DailyLimit => options.Value.HuggingFace.DailyLimit;

    public async Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        var o = options.Value.HuggingFace;
        var model = string.IsNullOrWhiteSpace(o.Model) ? "black-forest-labs/FLUX.1-schnell" : o.Model;
        var url = $"https://api-inference.huggingface.co/models/{model}";

        var payload = JsonSerializer.Serialize(new { inputs = brief.Prompt });
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null; // 503 = modelo aquecendo; demais = erro → próximo provedor
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null; // resposta JSON = erro/estimativa de tempo
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return bytes.Length > 256 ? bytes : null;
    }
}
