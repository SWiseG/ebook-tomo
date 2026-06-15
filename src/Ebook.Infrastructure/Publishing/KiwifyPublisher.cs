using System.Globalization;
using Ebook.Application.Publishing;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Ebook.Infrastructure.Publishing;

/// <summary>
/// Publica o produto na Kiwify via Playwright (E07-01), reaproveitando a sessão salva
/// (storageState — gere com <c>dotnet run --project src/Ebook.Api -- kiwify-login</c>).
/// Gated: sem credenciais/sessão, falha de forma tipada e o operador conclui manualmente.
///
/// ATENÇÃO: os seletores e o fluxo (em <see cref="KiwifySelectors"/>) são best-effort e
/// precisam ser VALIDADOS contra o dashboard real da Kiwify — não testável daqui.
/// </summary>
public sealed class KiwifyPublisher(
    IOptions<KiwifyOptions> options,
    IArtifactStore artifacts,
    ILogger<KiwifyPublisher> logger) : IKiwifyPublisher
{
    public async Task<Result<KiwifyPublishOutcome>> PublishAsync(KiwifyPublishRequest request, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.HasCredentials)
        {
            logger.LogWarning("Kiwify sem credenciais; publicação automática indisponível para {Slug}", request.Slug);
            return Result.Failure<KiwifyPublishOutcome>(PublishingErrors.NotConfigured);
        }

        if (!File.Exists(o.StorageStatePath))
        {
            logger.LogWarning("Sessão Kiwify ausente ({Path}); rode 'kiwify-login'", o.StorageStatePath);
            return Result.Failure<KiwifyPublishOutcome>(PublishingErrors.NotConfigured);
        }

        var pdf = await artifacts.ReadBytesAsync(request.PdfPath, ct);
        if (pdf is null)
        {
            return Result.Failure<KiwifyPublishOutcome>(PublishingErrors.AutomationPending);
        }

        var tempPdf = Path.Combine(Path.GetTempPath(), $"tomo-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(tempPdf, pdf, ct);
        try
        {
            return await DriveBrowserAsync(o, request, tempPdf, ct);
        }
        catch (PlaywrightException ex)
        {
            logger.LogError(ex, "Falha na automação Playwright da Kiwify para {Slug}", request.Slug);
            return Result.Failure<KiwifyPublishOutcome>(PublishingErrors.AutomationPending);
        }
        finally
        {
            TryDelete(tempPdf);
        }
    }

    private async Task<Result<KiwifyPublishOutcome>> DriveBrowserAsync(
        KiwifyOptions o, KiwifyPublishRequest request, string pdfPath, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = o.Headless });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = o.StorageStatePath
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{o.BaseUrl.TrimEnd('/')}/dashboard/products/new");
        await page.FillAsync(KiwifySelectors.ProductNameInput, request.Title);
        await page.FillAsync(KiwifySelectors.ProductPriceInput, request.Price.ToString("0.00", CultureInfo.InvariantCulture));
        await page.SetInputFilesAsync(KiwifySelectors.ProductFileUpload, pdfPath);
        await page.ClickAsync(KiwifySelectors.PublishButton);

        // aguarda a página de checkout aparecer e lê a URL
        await page.WaitForSelectorAsync(KiwifySelectors.CheckoutUrlField);
        var checkoutUrl = await page.GetAttributeAsync(KiwifySelectors.CheckoutUrlField, "value")
            ?? await page.InnerTextAsync(KiwifySelectors.CheckoutUrlField);

        if (string.IsNullOrWhiteSpace(checkoutUrl))
        {
            return Result.Failure<KiwifyPublishOutcome>(PublishingErrors.AutomationPending);
        }

        var kiwifyId = ExtractProductId(page.Url) ?? request.Slug;
        logger.LogInformation("Produto publicado na Kiwify via Playwright: {Slug} ({Id})", request.Slug, kiwifyId);
        return Result.Success(new KiwifyPublishOutcome(kiwifyId, checkoutUrl.Trim()));
    }

    // extrai o id do produto da URL (ex.: /dashboard/products/{id}/...) — best-effort
    private static string? ExtractProductId(string url)
    {
        var marker = "/products/";
        var i = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
        {
            return null;
        }

        var rest = url[(i + marker.Length)..].Split('/', '?');
        return rest.Length > 0 && rest[0].Length > 0 && rest[0] != "new" ? rest[0] : null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // melhor esforço
        }
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
