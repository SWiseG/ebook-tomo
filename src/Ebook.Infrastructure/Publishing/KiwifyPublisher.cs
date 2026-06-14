using Ebook.Application.Publishing;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Publishing;

/// <summary>
/// Publicação na Kiwify — COSTURA do E07. A automação real (Playwright) ainda não está
/// implementada; esta implementação falha de forma tipada para que o produto fique em
/// Publishing aguardando conclusão manual no painel (modo manual-assistido documentado).
/// Os seletores ficam centralizados em <see cref="KiwifySelectors"/> para a futura automação.
/// </summary>
public sealed class KiwifyPublisher(
    IOptions<KiwifyOptions> options,
    ILogger<KiwifyPublisher> logger) : IKiwifyPublisher
{
    public Task<Result<KiwifyPublishOutcome>> PublishAsync(KiwifyPublishRequest request, CancellationToken ct)
    {
        if (!options.Value.HasCredentials)
        {
            logger.LogWarning("Kiwify sem credenciais; publicação automática indisponível para {Slug}", request.Slug);
            return Task.FromResult(Result.Failure<KiwifyPublishOutcome>(PublishingErrors.NotConfigured));
        }

        // Seam: a automação Playwright (login via storageState, criação de produto, upload do PDF,
        // configuração de checkout) entra aqui, usando KiwifySelectors. Até lá, conclui-se manualmente.
        logger.LogWarning("Automação Playwright da Kiwify pendente; conclua a publicação de {Slug} manualmente", request.Slug);
        return Task.FromResult(Result.Failure<KiwifyPublishOutcome>(PublishingErrors.AutomationPending));
    }
}

/// <summary>
/// Seletores centralizados da Kiwify (E07 / mitigação de risco: quebra de layout em um só lugar).
/// Devem ser validados/ajustados contra o dashboard real antes de ligar a automação.
/// </summary>
public static class KiwifySelectors
{
    public const string EmailInput = "input[name='email']";
    public const string PasswordInput = "input[name='password']";
    public const string LoginSubmit = "button[type='submit']";
    public const string NewProductButton = "[data-testid='new-product']";
    public const string ProductNameInput = "input[name='name']";
    public const string ProductPriceInput = "input[name='price']";
    public const string ProductFileUpload = "input[type='file']";
    public const string PublishButton = "[data-testid='publish-product']";
    public const string CheckoutUrlField = "[data-testid='checkout-url']";
}
