using Ebook.Domain.Common;

namespace Ebook.Application.Publishing;

/// <summary>Tipos/chaves do job de publicação na Kiwify.</summary>
public static class PublishingJobs
{
    public const string Publish = "kiwify.publish";
    public const string Sync = "kiwify.sync";

    public static string PublishKey(Guid productId) => $"publish:{productId}";
    public static string SyncKey(Guid productId) => $"sync:{productId}";
}

public sealed record PublishJobPayload(Guid ProductId);

/// <summary>Dados necessários para criar o produto na Kiwify (nome, preço, arquivo, descrição).</summary>
public sealed record KiwifyPublishRequest(
    Guid ProductId,
    string Slug,
    string Title,
    string Description,
    decimal Price,
    string Currency,
    string PdfPath,
    string? LpUrl);

/// <summary>Resultado da publicação: id do produto na Kiwify + URL de checkout.</summary>
public sealed record KiwifyPublishOutcome(string KiwifyProductId, string CheckoutUrl);

/// <summary>
/// Contrato de publicação na Kiwify (E07). A implementação real (Playwright) é uma costura:
/// enquanto não estiver configurada, falha de forma tipada e o operador conclui manualmente.
/// </summary>
public interface IKiwifyPublisher
{
    Task<Result<KiwifyPublishOutcome>> PublishAsync(KiwifyPublishRequest request, CancellationToken ct);
}

public static class PublishingErrors
{
    public static Error ProductNotFound(Guid id) =>
        new("Publishing.Product.NotFound", $"Produto {id} não encontrado.");

    public static Error NotConfigured =>
        new("Publishing.Kiwify.NotConfigured",
            "Automação Kiwify não configurada. Publique manualmente e use “Concluir publicação”.");

    public static Error AutomationPending =>
        new("Publishing.Kiwify.AutomationPending",
            "Automação Playwright da Kiwify ainda não implementada. Publique manualmente e use “Concluir publicação”.");

    public static Error KiwifyApiNotConfigured =>
        new("Publishing.Kiwify.ApiNotConfigured",
            "API da Kiwify não configurada (client_id/secret/account_id). Defina via env Kiwify__*.");

    public static Error KiwifyApiUnavailable =>
        new("Publishing.Kiwify.ApiUnavailable", "Falha ao consultar a API da Kiwify.");

    public static Error KiwifyProductNotFound(string name) =>
        new("Publishing.Kiwify.ProductNotFound",
            $"Nenhum produto chamado \"{name}\" encontrado na Kiwify. Crie-o no dashboard primeiro.");

    public static Error KiwifyCheckoutMissing(string name) =>
        new("Publishing.Kiwify.CheckoutMissing",
            $"Produto \"{name}\" encontrado na Kiwify, mas sem link de checkout ativo. Configure o checkout no dashboard.");

    public static Error UnknownPlatform(string platform) =>
        new("Publishing.UnknownPlatform", $"Plataforma de publicação desconhecida: \"{platform}\".");

    public static Error NotSyncable(Domain.Products.ProductStatus status) =>
        new("Publishing.NotSyncable", $"Produto com status {status} não pode ser sincronizado.");
}
