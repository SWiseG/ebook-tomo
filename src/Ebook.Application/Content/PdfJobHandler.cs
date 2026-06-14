using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Pdf;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content;

/// <summary>
/// Etapa Pdf: renderiza o manuscrito aprovado em um PDF comercial (tema por nicho),
/// registra o artefato Pdf, avança o produto para Lp e enfileira a geração da landing page (E06).
/// Re-entrante: pula a renderização quando o PDF já existe.
/// </summary>
public sealed class PdfJobHandler(
    IProductRepository products,
    INicheRepository niches,
    IArtifactRepository artifacts,
    IPdfRenderer renderer,
    IFileStore fileStore,
    IArtifactStore artifactStore,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<PdfJobHandler> logger) : IJobHandler
{
    private const int PdfVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.Pdf;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<PdfJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(ContentErrors.ProductNotFound(payload.ProductId));
        }

        var pdfPath = ContentPaths.Pdf(product.Slug, PdfVersion);
        if (!artifactStore.Exists(pdfPath))
        {
            var rendered = await RenderAsync(product, pdfPath, ct);
            if (rendered.IsFailure)
            {
                return rendered;
            }
        }

        if (product.Stage == ProductStage.Pdf)
        {
            var advanced = product.AdvanceStage(); // Pdf → Lp
            if (advanced.IsFailure)
            {
                return Result.Failure(advanced.Error);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("PDF pronto para {Slug}", product.Slug);

        await jobQueue.EnqueueAsync(new JobRequest(
            ContentJobs.Lp,
            JsonSerializer.Serialize(new LpJobPayload(product.Id), JsonOptions),
            ContentJobs.LpKey(product.Id),
            ProductId: product.Id), ct);

        return Result.Success();
    }

    private async Task<Result> RenderAsync(Product product, string pdfPath, CancellationToken ct)
    {
        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1), ct);
        if (manuscript is null)
        {
            return Result.Failure(ContentErrors.ManuscriptMissing(product.Slug));
        }

        var outline = await ContentPaths.ReadOutlineAsync(fileStore, product.Slug, ct);
        var tagline = outline.IsSuccess ? outline.Value.Promise : null;

        var theme = PdfThemeSelector.ForNiche(await NicheSlugAsync(product, ct));
        var cta = await BuildCtaAsync(product, ct);
        var book = PdfBookComposer.Build(manuscript, product.Title, tagline, cta, theme);

        var coverImage = await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug), ct);
        var bytes = renderer.Render(book, coverImage);
        var stored = await artifactStore.WriteBytesAsync(pdfPath, bytes, ct);

        if (await artifacts.GetLatestAsync(product.Id, ArtifactType.Pdf, ct) is null)
        {
            var meta = JsonSerializer.Serialize(new { theme = theme.ToString(), bytes = stored.SizeBytes }, JsonOptions);
            artifacts.Add(Artifact.Create(
                product.Id, ArtifactType.Pdf, stored.RelativePath, stored.Sha256, PdfVersion, meta, clock.UtcNow));
        }

        logger.LogInformation("PDF renderizado para {Slug} ({Bytes} bytes, tema {Theme})",
            product.Slug, stored.SizeBytes, theme);
        return Result.Success();
    }

    private async Task<string> NicheSlugAsync(Product product, CancellationToken ct)
    {
        var niche = await niches.GetByIdAsync(product.NicheId, ct);
        return niche?.Slug ?? product.Slug;
    }

    private async Task<PdfCta> BuildCtaAsync(Product product, CancellationToken ct)
    {
        var headline = product.Title;
        var salesCopy = await fileStore.ReadTextAsync(ContentPaths.SalesCopy(product.Slug), ct);
        if (salesCopy is not null)
        {
            var parsed = AiJson.Parse<SalesCopyDto>(salesCopy, "ebook.sales-copy");
            if (parsed.IsSuccess && !string.IsNullOrWhiteSpace(parsed.Value.Headline))
            {
                headline = parsed.Value.Headline;
            }
        }

        return new PdfCta(headline, product.CheckoutUrl);
    }
}
