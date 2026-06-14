using Ebook.Domain.Common;

namespace Ebook.Application.Publishing;

/// <summary>Tipos/chaves do job de publicação na Kiwify.</summary>
public static class PublishingJobs
{
    public const string Publish = "kiwify.publish";

    public static string PublishKey(Guid productId) => $"publish:{productId}";
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
}
