using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;
using Ebook.Domain.Social;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Social;

/// <summary>
/// Publica um post social via <see cref="ISocialPublisher"/> e marca Published (com id externo).
/// Falha gated (Meta não configurado) → marca o post como Failed e encerra (sem dead-letter).
/// Re-entrante: no-op quando o post não está Queued.
/// </summary>
public sealed class PublishPostJobHandler(
    ISocialPostRepository posts,
    IProductRepository products,
    IChannelRepository channels,
    ISocialPublisher publisher,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<PublishPostJobHandler> logger) : IJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => SocialJobs.Publish;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<PublishPostJobPayload>(payloadJson, JsonOptions)!;
        var post = await posts.GetByIdAsync(payload.PostId, ct);
        if (post is null)
        {
            return Result.Success(); // post removido: nada a fazer
        }

        if (post.Status != SocialPostStatus.Queued)
        {
            return Result.Success(); // já publicado/falhou ou fora de ordem
        }

        var product = await products.GetByIdAsync(post.ProductId, ct);
        var caption = string.IsNullOrWhiteSpace(post.Hashtags) ? post.Caption : $"{post.Caption}\n\n{post.Hashtags}";
        var link = product?.CheckoutUrl ?? product?.LpUrl;

        // roteia pelo canal do nicho (1 conta por nicho); sem canal conectado, cai no Meta global.
        ChannelCredentials? channelCreds = null;
        if (product is not null)
        {
            var channel = await channels.GetByNicheAsync(product.NicheId, ct);
            if (channel is { IsConnected: true })
            {
                channelCreds = new ChannelCredentials(
                    channel.PageId, channel.IgUserId, channel.AccessToken!, channel.PublicMediaBaseUrl);
            }
        }

        var carousel = string.IsNullOrWhiteSpace(post.CarouselPaths)
            ? null
            : post.CarouselPaths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var outcome = await publisher.PublishAsync(
            new SocialPublishRequest(post.Network, caption, post.MediaPath, link, channelCreds, carousel), ct);

        if (outcome.IsFailure)
        {
            // falha gated/permanente: marca como falho (visível no painel) sem dead-letter
            post.MarkFailed();
            await unitOfWork.SaveChangesAsync(ct);
            logger.LogWarning("Falha ao publicar post {PostId}: {Error}", post.Id, outcome.Error.Code);
            return Result.Success();
        }

        var marked = post.MarkPublished(outcome.Value.ExternalId, clock.UtcNow);
        if (marked.IsFailure)
        {
            return Result.Failure(marked.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Post {PostId} publicado em {Network} ({ExternalId})",
            post.Id, post.Network, outcome.Value.ExternalId);
        return Result.Success();
    }
}
