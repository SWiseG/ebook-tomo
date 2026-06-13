using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content;

/// <summary>
/// Etapa Pdf (pré-render): gera a capa do e-book e o mockup 3D de marketing (E09),
/// indexa os artefatos e enfileira a renderização do PDF (que embute a capa). Não muda o estágio.
/// Re-entrante: pula a geração quando a capa já existe.
/// </summary>
public sealed class CoverJobHandler(
    IProductRepository products,
    INicheRepository niches,
    IArtifactRepository artifacts,
    IImageComposer composer,
    IPhotoProvider photos,
    IFileStore fileStore,
    IArtifactStore artifactStore,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<CoverJobHandler> logger) : IJobHandler
{
    private const int Version = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.Cover;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<CoverJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(ContentErrors.ProductNotFound(payload.ProductId));
        }

        if (!artifactStore.Exists(ContentPaths.Cover(product.Slug)))
        {
            await RenderAsync(product, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        await jobQueue.EnqueueAsync(new JobRequest(
            ContentJobs.Pdf,
            JsonSerializer.Serialize(new PdfJobPayload(product.Id), JsonOptions),
            ContentJobs.PdfKey(product.Id),
            ProductId: product.Id), ct);

        return Result.Success();
    }

    private async Task RenderAsync(Product product, CancellationToken ct)
    {
        var niche = await niches.GetByIdAsync(product.NicheId, ct);
        var nicheSlug = niche?.Slug ?? product.Slug;
        var palette = await ResolvePaletteAsync(nicheSlug, ct);

        var outline = await ContentPaths.ReadOutlineAsync(fileStore, product.Slug, ct);
        var title = outline.IsSuccess ? outline.Value.Title : product.Title;
        var subtitle = outline.IsSuccess ? outline.Value.Subtitle : null;

        var photo = await photos.TryGetBackgroundAsync(niche?.Name ?? product.Title, ct);

        var coverPng = composer.RenderCover(new CoverArt(title, subtitle, niche?.Name, palette), photo);
        var coverStored = await artifactStore.WriteBytesAsync(ContentPaths.Cover(product.Slug), coverPng, ct);
        await AddArtifactIfNewAsync(product.Id, ArtifactType.Cover, coverStored, ct);

        var mockupPng = composer.RenderMockup(coverPng, palette);
        var mockupStored = await artifactStore.WriteBytesAsync(ContentPaths.Mockup(product.Slug), mockupPng, ct);
        await AddArtifactIfNewAsync(product.Id, ArtifactType.Mockup, mockupStored, ct);

        logger.LogInformation("Capa e mockup gerados para {Slug} (foto de fundo: {HasPhoto})",
            product.Slug, photo is not null);
    }

    private async Task<NichePalette> ResolvePaletteAsync(string nicheSlug, CancellationToken ct)
    {
        var config = await fileStore.ReadTextAsync(ContentPaths.PaletteConfig(nicheSlug), ct);
        if (config is not null)
        {
            var parsed = AiJson.Parse<NichePalette>(config, "palette");
            if (parsed.IsSuccess && !string.IsNullOrWhiteSpace(parsed.Value.Background))
            {
                return parsed.Value;
            }
        }

        return PaletteCatalog.ForNiche(nicheSlug);
    }

    private async Task AddArtifactIfNewAsync(Guid productId, ArtifactType type, StoredFile stored, CancellationToken ct)
    {
        if (await artifacts.GetLatestAsync(productId, type, ct) is null)
        {
            artifacts.Add(Artifact.Create(
                productId, type, stored.RelativePath, stored.Sha256, Version, "{}", clock.UtcNow));
        }
    }
}
