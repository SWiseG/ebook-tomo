using System.Text.Json;
using Ebook.Application.Social;
using Ebook.Domain.Common;
using Ebook.Domain.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Social;

/// <summary>
/// Publicação no Meta (Instagram/Facebook) via Graph API (E08-01 / E10-04). Imagem no Instagram:
/// cria o container (image_url + caption) e publica (media_publish). Reel: container REELS
/// (video_url), aguarda o processamento (FINISHED) e publica. Facebook: foto/vídeo na Página.
/// Exige conta IG Business + app Meta aprovado + token de longa duração (gated por config).
/// Não testável contra o Meta real daqui — a construção das requisições é coberta por testes.
/// </summary>
public sealed class MetaGraphPublisher(
    HttpClient http,
    IOptions<MetaOptions> options,
    ILogger<MetaGraphPublisher> logger) : ISocialPublisher
{
    public async Task<Result<SocialPublishOutcome>> PublishAsync(SocialPublishRequest request, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.HasCredentials)
        {
            logger.LogWarning("Meta sem credenciais; publicação em {Network} indisponível", request.Network);
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured);
        }

        if (string.IsNullOrWhiteSpace(request.MediaPath) || !o.MediaConfigured)
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured);
        }

        var mediaUrl = $"{o.PublicMediaBaseUrl.TrimEnd('/')}/media/{request.MediaPath}";
        var isVideo = request.MediaPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

        return request.Network switch
        {
            SocialNetwork.Facebook => await PublishFacebookAsync(o, request, mediaUrl, isVideo, ct),
            SocialNetwork.Instagram => await PublishInstagramAsync(o, request, mediaUrl, isVideo, ct),
            _ => Result.Failure<SocialPublishOutcome>(SocialErrorsApp.AutomationPending) // X em P1
        };
    }

    private async Task<Result<SocialPublishOutcome>> PublishInstagramAsync(
        MetaOptions o, SocialPublishRequest request, string mediaUrl, bool isVideo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(o.IgUserId))
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured);
        }

        var container = new Dictionary<string, string> { ["caption"] = request.Caption, ["access_token"] = o.AccessToken };
        if (isVideo)
        {
            container["media_type"] = "REELS";
            container["video_url"] = mediaUrl;
        }
        else
        {
            container["image_url"] = mediaUrl;
        }

        var created = await PostAsync(Url(o, o.IgUserId, "media"), container, ct);
        if (created.IsFailure)
        {
            return Result.Failure<SocialPublishOutcome>(created.Error);
        }

        var creationId = GetString(created.Value, "id");
        if (creationId is null)
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.AutomationPending);
        }

        if (isVideo)
        {
            var ready = await WaitForContainerAsync(o, creationId, ct);
            if (ready.IsFailure)
            {
                return Result.Failure<SocialPublishOutcome>(ready.Error);
            }
        }

        var published = await PostAsync(
            Url(o, o.IgUserId, "media_publish"),
            new Dictionary<string, string> { ["creation_id"] = creationId, ["access_token"] = o.AccessToken },
            ct);
        if (published.IsFailure)
        {
            return Result.Failure<SocialPublishOutcome>(published.Error);
        }

        var id = GetString(published.Value, "id") ?? creationId;
        logger.LogInformation("Publicado no Instagram ({Id})", id);
        return Result.Success(new SocialPublishOutcome(id));
    }

    private async Task<Result<SocialPublishOutcome>> PublishFacebookAsync(
        MetaOptions o, SocialPublishRequest request, string mediaUrl, bool isVideo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(o.PageId))
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured);
        }

        var edge = isVideo ? "videos" : "photos";
        var body = new Dictionary<string, string> { ["access_token"] = o.AccessToken };
        if (isVideo)
        {
            body["file_url"] = mediaUrl;
            body["description"] = request.Caption;
        }
        else
        {
            body["url"] = mediaUrl;
            body["caption"] = request.Caption;
        }

        var posted = await PostAsync(Url(o, o.PageId, edge), body, ct);
        if (posted.IsFailure)
        {
            return Result.Failure<SocialPublishOutcome>(posted.Error);
        }

        var id = GetString(posted.Value, "post_id") ?? GetString(posted.Value, "id");
        if (id is null)
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.AutomationPending);
        }

        logger.LogInformation("Publicado no Facebook ({Id})", id);
        return Result.Success(new SocialPublishOutcome(id));
    }

    private async Task<Result> WaitForContainerAsync(MetaOptions o, string creationId, CancellationToken ct)
    {
        var statusUrl = $"{o.ApiBase}/{o.GraphApiVersion}/{creationId}?fields=status_code&access_token={o.AccessToken}";
        for (var attempt = 0; attempt < o.ReelPollAttempts; attempt++)
        {
            using var response = await http.GetAsync(statusUrl, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                var status = GetString(doc.RootElement, "status_code");
                if (status == "FINISHED")
                {
                    return Result.Success();
                }

                if (status == "ERROR")
                {
                    return Result.Failure(VideoContainerError);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(o.ReelPollDelaySeconds), ct);
        }

        return Result.Failure(VideoContainerError);
    }

    private async Task<Result<JsonElement>> PostAsync(string url, Dictionary<string, string> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync(url, content, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Result.Failure<JsonElement>(new Error("Social.Meta.BadResponse", "Resposta não-JSON da Graph API."));
        }

        var root = doc.RootElement.Clone();
        doc.Dispose();

        if (!response.IsSuccessStatusCode || root.TryGetProperty("error", out _))
        {
            var message = root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var m)
                ? m.GetString()
                : $"HTTP {(int)response.StatusCode}";
            logger.LogWarning("Graph API falhou: {Message}", message);
            return Result.Failure<JsonElement>(new Error("Social.Meta.ApiError", message ?? "erro Graph API"));
        }

        return Result.Success(root);
    }

    private static string Url(MetaOptions o, string node, string edge) =>
        $"{o.ApiBase}/{o.GraphApiVersion}/{node}/{edge}";

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static readonly Error VideoContainerError =
        new("Social.Meta.ContainerFailed", "O container de Reel não ficou pronto a tempo.");
}
