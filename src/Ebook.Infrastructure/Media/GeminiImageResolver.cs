using System.Text;
using System.Text.Json;
using Ebook.Application.Media;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// Imagen via Gemini API / AI Studio (E14-02). Gated pela chave (`Media:Gemini:ApiKey`). Endpoint
/// `:predict` → predictions[].bytesBase64Encoded. Best-effort: contrato pode variar por modelo,
/// então o parse é defensivo e qualquer falha vira null (o gateway tenta o próximo provedor).
/// </summary>
public sealed class GeminiImageResolver(HttpClient http, IOptions<MediaOptions> options) : IMediaResolver
{
    public MediaProvider Provider => MediaProvider.Gemini;
    public bool Enabled => options.Value.Gemini.Enabled && !string.IsNullOrWhiteSpace(options.Value.Gemini.ApiKey);
    public int DailyLimit => options.Value.Gemini.DailyLimit;

    public async Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        var o = options.Value.Gemini;
        var model = string.IsNullOrWhiteSpace(o.Model) ? "imagen-3.0-generate-002" : o.Model;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:predict";

        var payload = JsonSerializer.Serialize(new
        {
            instances = new[] { new { prompt = brief.Prompt } },
            parameters = new { sampleCount = 1, aspectRatio = AspectRatio(brief.Width, brief.Height) },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-goog-api-key", o.ApiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("predictions", out var preds) && preds.GetArrayLength() > 0
            && preds[0].TryGetProperty("bytesBase64Encoded", out var b64) && b64.ValueKind == JsonValueKind.String)
        {
            try
            {
                return Convert.FromBase64String(b64.GetString()!);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return null;
    }

    // Imagen aceita razão de aspecto (não px); mapeia a partir das dimensões do brief.
    private static string AspectRatio(int width, int height)
    {
        var ratio = height == 0 ? 1d : (double)width / height;
        return ratio switch
        {
            <= 0.6 => "9:16",
            <= 0.85 => "3:4",
            < 1.2 => "1:1",
            < 1.6 => "4:3",
            _ => "16:9",
        };
    }
}
