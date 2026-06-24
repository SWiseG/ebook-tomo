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
        // default = Nano Banana (Gemini 2.5 Flash Image). Modelos "gemini-*" usam generateContent;
        // "imagen-*" usam :predict. Endpoint e parse divergem por família.
        var model = string.IsNullOrWhiteSpace(o.Model) ? "gemini-2.5-flash-image" : o.Model;
        var isGemini = model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase);

        var url = isGemini
            ? $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
            : $"https://generativelanguage.googleapis.com/v1beta/models/{model}:predict";

        var aspect = AspectRatio(brief.Width, brief.Height);
        var payload = isGemini
            ? JsonSerializer.Serialize(new
            {
                contents = new[] { new { parts = new[] { new { text = $"{brief.Prompt} Aspect ratio {aspect}." } } } },
                generationConfig = new { responseModalities = new[] { "IMAGE" } },
            })
            : JsonSerializer.Serialize(new
            {
                instances = new[] { new { prompt = brief.Prompt } },
                parameters = new { sampleCount = 1, aspectRatio = aspect },
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
        var b64 = isGemini ? ExtractGemini(doc.RootElement) : ExtractImagen(doc.RootElement);
        if (b64 is null)
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(b64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // imagen :predict → predictions[0].bytesBase64Encoded
    private static string? ExtractImagen(JsonElement root) =>
        root.TryGetProperty("predictions", out var preds) && preds.GetArrayLength() > 0
        && preds[0].TryGetProperty("bytesBase64Encoded", out var b) && b.ValueKind == JsonValueKind.String
            ? b.GetString()
            : null;

    // gemini generateContent → candidates[0].content.parts[].inlineData.data
    private static string? ExtractGemini(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0
            || !cands[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts))
        {
            return null;
        }

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("inlineData", out var inline)
                && inline.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.String)
            {
                return d.GetString();
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
