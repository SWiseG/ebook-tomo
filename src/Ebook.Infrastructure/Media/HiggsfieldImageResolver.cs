using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Ebook.Application.Media;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// Higgsfield Soul (text2image) — gerador fotorrealista de alta qualidade (E14, premium pago).
/// Gated por <c>Media:Higgsfield:ApiKey</c> (KEY_ID) + <c>Secret</c> (KEY_SECRET). Fluxo assíncrono:
/// POST <c>/v1/text2image/soul</c> → polling em <c>/requests/{id}/status</c> até <c>completed</c> →
/// baixa <c>images[0].url</c>. Parse defensivo (contrato pode variar por modelo); qualquer falha →
/// null e o gateway tenta o próximo provedor. Nunca lança.
/// </summary>
public sealed class HiggsfieldImageResolver(HttpClient http, IOptions<MediaOptions> options) : IMediaResolver
{
    private const string BaseUrl = "https://platform.higgsfield.ai";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MediaProvider Provider => MediaProvider.Higgsfield;

    public bool Enabled => options.Value.Higgsfield.Enabled
        && !string.IsNullOrWhiteSpace(options.Value.Higgsfield.ApiKey)
        && !string.IsNullOrWhiteSpace(options.Value.Higgsfield.Secret);

    public int DailyLimit => options.Value.Higgsfield.DailyLimit;

    public async Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        var o = options.Value.Higgsfield;
        var auth = $"Key {o.ApiKey}:{o.Secret}";

        var payload = JsonSerializer.Serialize(new
        {
            @params = new
            {
                prompt = brief.Prompt,
                width_and_height = SoulSize(brief.Width, brief.Height),
                quality = "1080p",
                batch_size = 1,
            },
        });

        // 1. submete o job
        using var submit = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/text2image/soul")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        submit.Headers.TryAddWithoutValidation("Authorization", auth);

        using var submitResponse = await http.SendAsync(submit, ct);
        if (!submitResponse.IsSuccessStatusCode)
        {
            return null;
        }

        string statusUrl;
        await using (var s = await submitResponse.Content.ReadAsStreamAsync(ct))
        using (var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct))
        {
            var url = ResolveStatusUrl(doc.RootElement);
            if (url is null)
            {
                return null;
            }

            statusUrl = url;
        }

        // 2. polling até concluir (teto ~120s; o provedor leva alguns segundos)
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(120))
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);

            using var poll = new HttpRequestMessage(HttpMethod.Get, statusUrl);
            poll.Headers.TryAddWithoutValidation("Authorization", auth);

            using var pollResponse = await http.SendAsync(poll, ct);
            if (!pollResponse.IsSuccessStatusCode)
            {
                continue;
            }

            await using var ps = await pollResponse.Content.ReadAsStreamAsync(ct);
            using var pdoc = await JsonDocument.ParseAsync(ps, cancellationToken: ct);
            var status = StatusOf(pdoc.RootElement);

            if (status is "failed" or "nsfw" or "canceled")
            {
                return null;
            }

            if (status is "completed")
            {
                var imageUrl = FirstImageUrl(pdoc.RootElement);
                return imageUrl is null ? null : await DownloadAsync(imageUrl, ct);
            }
            // queued / in_progress → continua
        }

        return null; // timeout
    }

    // Aceita um status_url absoluto da resposta ou monta /requests/{id}/status a partir de id/request_id.
    private static string? ResolveStatusUrl(JsonElement root)
    {
        if (TryGetString(root, "status_url", out var abs) && Uri.IsWellFormedUriString(abs, UriKind.Absolute))
        {
            return abs;
        }

        foreach (var key in new[] { "id", "request_id", "generation_id" })
        {
            if (TryGetString(root, key, out var id) && !string.IsNullOrWhiteSpace(id))
            {
                return $"{BaseUrl}/requests/{id}/status";
            }
        }

        // alguns retornos embrulham num array de jobs
        if (root.TryGetProperty("jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array && jobs.GetArrayLength() > 0
            && TryGetString(jobs[0], "id", out var jobId))
        {
            return $"{BaseUrl}/requests/{jobId}/status";
        }

        return null;
    }

    private static string? StatusOf(JsonElement root)
    {
        if (TryGetString(root, "status", out var s))
        {
            return s.ToLowerInvariant();
        }

        if (root.TryGetProperty("jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array && jobs.GetArrayLength() > 0
            && TryGetString(jobs[0], "status", out var js))
        {
            return js.ToLowerInvariant();
        }

        return null;
    }

    // completed → images[0].url (ou jobs[0].results.raw.url).
    private static string? FirstImageUrl(JsonElement root)
    {
        if (root.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array
            && images.GetArrayLength() > 0 && TryGetString(images[0], "url", out var u))
        {
            return u;
        }

        if (root.TryGetProperty("jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array && jobs.GetArrayLength() > 0
            && jobs[0].TryGetProperty("results", out var results)
            && results.TryGetProperty("raw", out var raw)
            && TryGetString(raw, "url", out var r))
        {
            return r;
        }

        return null;
    }

    private async Task<byte[]?> DownloadAsync(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return bytes.Length > 256 ? bytes : null;
    }

    private static bool TryGetString(JsonElement el, string name, out string value)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
        {
            value = p.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    // Soul aceita só dimensões fixas; mapeia o brief para a mais próxima por orientação.
    private static string SoulSize(int width, int height) =>
        width == height ? "1536x1536" : width < height ? "1536x2048" : "2048x1536";
}
