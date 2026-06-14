using Ebook.Application.Social;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Social;

/// <summary>
/// Publicação no Meta (Instagram/Facebook) — COSTURA do E08. A integração real com a Graph API
/// (container de mídia + media_publish; exige app aprovado + conta IG Business) ainda não está
/// implementada; falha de forma tipada para o post ficar visível como "Falhou" no painel.
/// Endpoints centralizados em <see cref="MetaEndpoints"/> para a futura implementação.
/// </summary>
public sealed class MetaGraphPublisher(
    IOptions<MetaOptions> options,
    ILogger<MetaGraphPublisher> logger) : ISocialPublisher
{
    public Task<Result<SocialPublishOutcome>> PublishAsync(SocialPublishRequest request, CancellationToken ct)
    {
        if (!options.Value.HasCredentials)
        {
            logger.LogWarning("Meta sem credenciais; publicação em {Network} indisponível", request.Network);
            return Task.FromResult(Result.Failure<SocialPublishOutcome>(SocialErrorsApp.NotConfigured));
        }

        // Seam: criar container (POST /{ig-user-id}/media com image_url + caption) e publicar
        // (POST /{ig-user-id}/media_publish). A imagem precisa de URL pública (LP estática).
        logger.LogWarning("Publicação Meta pendente (Graph API não implementada) para {Network}", request.Network);
        return Task.FromResult(Result.Failure<SocialPublishOutcome>(SocialErrorsApp.AutomationPending));
    }
}

/// <summary>Endpoints da Graph API centralizados (E08) — validar versão/forma na implementação real.</summary>
public static class MetaEndpoints
{
    public static string IgCreateMedia(string apiBase, string version, string igUserId) =>
        $"{apiBase}/{version}/{igUserId}/media";

    public static string IgPublishMedia(string apiBase, string version, string igUserId) =>
        $"{apiBase}/{version}/{igUserId}/media_publish";

    public static string PageFeed(string apiBase, string version, string pageId) =>
        $"{apiBase}/{version}/{pageId}/photos";
}
