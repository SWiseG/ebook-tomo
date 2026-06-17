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
        // credenciais do canal (por nicho) têm prioridade; sem canal, cai no Meta global (legado).
        var c = request.Channel is not null
            ? new MetaCreds(request.Channel.PageId, request.Channel.IgUserId, request.Channel.AccessToken, request.Channel.PublicMediaBaseUrl)
            : new MetaCreds(o.PageId, o.IgUserId, o.AccessToken, o.PublicMediaBaseUrl);

        if (!c.HasCredentials)
        {
            logger.LogWarning("Meta sem credenciais; publicação em {Network} indisponível", request.Network);
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured);
        }

        if (string.IsNullOrWhiteSpace(request.MediaPath) || string.IsNullOrWhiteSpace(c.MediaBase))
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured);
        }

        var mediaUrl = $"{c.MediaBase.TrimEnd('/')}/media/{request.MediaPath}";
        var isVideo = request.MediaPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

        return request.Network switch
        {
            SocialNetwork.Facebook => await PublishFacebookAsync(o, c, request, mediaUrl, isVideo, ct),
            SocialNetwork.Instagram => await PublishInstagramAsync(o, c, request, mediaUrl, isVideo, ct),
            _ => Result.Failure<SocialPublishOutcome>(SocialErrorsApp.AutomationPending) // X em P1
        };
    }

    private async Task<Result<SocialPublishOutcome>> PublishInstagramAsync(
        MetaOptions o, MetaCreds c, SocialPublishRequest request, string mediaUrl, bool isVideo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(c.IgUserId))
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured);
        }

        // carrossel (E09): múltiplas imagens → containers filhos + container pai CAROUSEL.
        if (request.CarouselPaths is { Count: > 1 } && !isVideo)
        {
            return await PublishInstagramCarouselAsync(o, c, request, ct);
        }

        var container = new Dictionary<string, string> { ["caption"] = request.Caption, ["access_token"] = c.AccessToken };
        if (isVideo)
        {
            container["media_type"] = "REELS";
            container["video_url"] = mediaUrl;
        }
        else
        {
            container["image_url"] = mediaUrl;
        }

        var created = await PostAsync(Url(o, c.IgUserId!, "media"), container, ct);
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
            var ready = await WaitForContainerAsync(o, c, creationId, ct);
            if (ready.IsFailure)
            {
                return Result.Failure<SocialPublishOutcome>(ready.Error);
            }
        }

        var published = await PostAsync(
            Url(o, c.IgUserId!, "media_publish"),
            new Dictionary<string, string> { ["creation_id"] = creationId, ["access_token"] = c.AccessToken },
            ct);
        if (published.IsFailure)
        {
            return Result.Failure<SocialPublishOutcome>(published.Error);
        }

        var id = GetString(published.Value, "id") ?? creationId;
        logger.LogInformation("Publicado no Instagram ({Id})", id);
        return Result.Success(new SocialPublishOutcome(id));
    }

    private async Task<Result<SocialPublishOutcome>> PublishInstagramCarouselAsync(
        MetaOptions o, MetaCreds c, SocialPublishRequest request, CancellationToken ct)
    {
        var childIds = new List<string>();
        foreach (var path in request.CarouselPaths!)
        {
            var url = $"{c.MediaBase!.TrimEnd('/')}/media/{path}";
            var child = await PostAsync(Url(o, c.IgUserId!, "media"), new Dictionary<string, string>
            {
                ["image_url"] = url,
                ["is_carousel_item"] = "true",
                ["access_token"] = c.AccessToken,
            }, ct);
            if (child.IsFailure)
            {
                return Result.Failure<SocialPublishOutcome>(child.Error);
            }

            var childId = GetString(child.Value, "id");
            if (childId is null)
            {
                return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.AutomationPending);
            }

            childIds.Add(childId);
        }

        var parent = await PostAsync(Url(o, c.IgUserId!, "media"), new Dictionary<string, string>
        {
            ["media_type"] = "CAROUSEL",
            ["children"] = string.Join(',', childIds),
            ["caption"] = request.Caption,
            ["access_token"] = c.AccessToken,
        }, ct);
        if (parent.IsFailure)
        {
            return Result.Failure<SocialPublishOutcome>(parent.Error);
        }

        var creationId = GetString(parent.Value, "id");
        if (creationId is null)
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.AutomationPending);
        }

        var published = await PostAsync(Url(o, c.IgUserId!, "media_publish"), new Dictionary<string, string>
        {
            ["creation_id"] = creationId,
            ["access_token"] = c.AccessToken,
        }, ct);
        if (published.IsFailure)
        {
            return Result.Failure<SocialPublishOutcome>(published.Error);
        }

        var id = GetString(published.Value, "id") ?? creationId;
        logger.LogInformation("Carrossel publicado no Instagram ({Id}, {Count} slides)", id, childIds.Count);
        return Result.Success(new SocialPublishOutcome(id));
    }

    private async Task<Result<SocialPublishOutcome>> PublishFacebookAsync(
        MetaOptions o, MetaCreds c, SocialPublishRequest request, string mediaUrl, bool isVideo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(c.PageId))
        {
            return Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured);
        }

        var edge = isVideo ? "videos" : "photos";
        var body = new Dictionary<string, string> { ["access_token"] = c.AccessToken };
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

        var posted = await PostAsync(Url(o, c.PageId!, edge), body, ct);
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

    private async Task<Result> WaitForContainerAsync(MetaOptions o, MetaCreds c, string creationId, CancellationToken ct)
    {
        var statusUrl = $"{o.ApiBase}/{o.GraphApiVersion}/{creationId}?fields=status_code&access_token={c.AccessToken}";
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

    /// <summary>Credenciais efetivas da publicação (do canal do nicho ou do Meta global).</summary>
    private sealed record MetaCreds(string? PageId, string? IgUserId, string AccessToken, string? MediaBase)
    {
        public bool HasCredentials =>
            !string.IsNullOrWhiteSpace(AccessToken)
            && (!string.IsNullOrWhiteSpace(IgUserId) || !string.IsNullOrWhiteSpace(PageId));
    }
}
