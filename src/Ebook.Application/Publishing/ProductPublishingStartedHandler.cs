using System.Text.Json;
using Ebook.Application.Common.Events;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Publishing;

/// <summary>
/// Quando a publicação inicia (aprovação manual ou modo Auto), enfileira o job de
/// publicação na Kiwify — desde que <c>kiwify.autoPublish</c> esteja ligado. Caso contrário,
/// o produto fica em Publishing aguardando conclusão manual no painel (modo manual-assistido).
/// </summary>
public sealed class ProductPublishingStartedHandler(
    IJobQueue jobQueue,
    ISettingsStore settings,
    ILogger<ProductPublishingStartedHandler> logger) : IDomainEventHandler<ProductPublishingStarted>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(ProductPublishingStarted domainEvent, CancellationToken ct)
    {
        var autoPublish = await settings.GetOrDefaultAsync(SettingKeys.KiwifyAutoPublish, false, ct);
        if (!autoPublish)
        {
            logger.LogInformation(
                "Produto {ProductId} em publicação; modo manual-assistido (kiwify.autoPublish=false).",
                domainEvent.ProductId);
            return;
        }

        await jobQueue.EnqueueAsync(new JobRequest(
            PublishingJobs.Publish,
            JsonSerializer.Serialize(new PublishJobPayload(domainEvent.ProductId), JsonOptions),
            PublishingJobs.PublishKey(domainEvent.ProductId),
            ProductId: domainEvent.ProductId), ct);
    }
}
