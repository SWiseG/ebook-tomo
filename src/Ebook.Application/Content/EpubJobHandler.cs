using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content;

/// <summary>
/// Gera o EPUB 3 do produto a partir do manuscrito aprovado e o indexa como artefato.
/// Enfileirado pelo CoverJobHandler em paralelo com o PDF. Re-entrante: pula se artefato já existe.
/// Não muda o estágio do produto (responsabilidade do PdfJobHandler).
/// </summary>
public sealed class EpubJobHandler(
    IProductRepository products,
    INicheRepository niches,
    IArtifactRepository artifacts,
    IEbookExporter exporter,
    IFileStore fileStore,
    IArtifactStore artifactStore,
    IPaletteResolver paletteResolver,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<EpubJobHandler> logger) : IJobHandler
{
    private const int EpubVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.Epub;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<EpubJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(ContentErrors.ProductNotFound(payload.ProductId));
        }

        var epubPath = ContentPaths.Epub(product.Slug, EpubVersion);
        if (artifactStore.Exists(epubPath))
        {
            logger.LogInformation("EPUB já existe para {Slug}; pulando", product.Slug);
            return Result.Success();
        }

        var rendered = await RenderAsync(product, epubPath, ct);
        if (rendered.IsFailure)
        {
            return rendered;
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("EPUB gerado para {Slug}", product.Slug);
        return Result.Success();
    }

    private async Task<Result> RenderAsync(Product product, string epubPath, CancellationToken ct)
    {
        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1), ct);
        if (manuscript is null)
        {
            return Result.Failure(ContentErrors.ManuscriptMissing(product.Slug));
        }

        var outline = await ContentPaths.ReadOutlineAsync(fileStore, product.Slug, ct);
        var tagline = outline.IsSuccess ? outline.Value.Promise : null;

        var niche = await niches.GetByIdAsync(product.NicheId, ct);
        var nicheSlug = niche?.Slug ?? product.Slug;
        var theme = PdfThemeSelector.ForNiche(nicheSlug);
        var palette = await paletteResolver.ResolveAsync(product.Slug, nicheSlug, ct);

        var salesCopy = await fileStore.ReadTextAsync(ContentPaths.SalesCopy(product.Slug), ct);
        string ctaHeadline = product.Title;
        if (salesCopy is not null)
        {
            var copy = AiJson.Parse<SalesCopyDto>(salesCopy, "ebook.sales-copy");
            if (copy.IsSuccess && !string.IsNullOrWhiteSpace(copy.Value.Headline))
            {
                ctaHeadline = copy.Value.Headline;
            }
        }

        var cta = new PdfCta(ctaHeadline, product.CheckoutUrl);
        var book = PdfBookComposer.Build(manuscript, product.Title, tagline, cta, theme, palette);

        var coverImage = await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug), ct);
        var bytes = exporter.ExportEpub(book, coverImage);
        var stored = await artifactStore.WriteBytesAsync(epubPath, bytes, ct);

        if (await artifacts.GetLatestAsync(product.Id, ArtifactType.Epub, ct) is null)
        {
            var meta = JsonSerializer.Serialize(new { bytes = stored.SizeBytes }, JsonOptions);
            artifacts.Add(Artifact.Create(
                product.Id, ArtifactType.Epub, stored.RelativePath, stored.Sha256,
                EpubVersion, meta, clock.UtcNow));
        }

        logger.LogInformation("EPUB renderizado para {Slug} ({Bytes} bytes)", product.Slug, stored.SizeBytes);
        return Result.Success();
    }
}
