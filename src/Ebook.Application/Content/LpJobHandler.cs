using System.Text;
using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Lp;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content;

/// <summary>
/// Etapa Lp (E06): preenche um template de landing page com a copy e a capa do produto,
/// publica o bundle HTML auto-contido e submete o produto à aprovação (ou publica direto
/// no modo Auto). Encerra o pipeline de conteúdo — costura para o E07 (Kiwify Publisher).
/// Re-entrante: pula a renderização quando o bundle já existe e a transição quando já ocorreu.
/// </summary>
public sealed class LpJobHandler(
    IProductRepository products,
    INicheRepository niches,
    IArtifactRepository artifacts,
    IFileStore fileStore,
    IArtifactStore artifactStore,
    ISettingsStore settings,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<LpJobHandler> logger) : IJobHandler
{
    private const int Version = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.Lp;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<LpJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(ContentErrors.ProductNotFound(payload.ProductId));
        }

        var baseUrl = (await settings.GetOrDefaultAsync(SettingKeys.LpBaseUrl, string.Empty, ct)).TrimEnd('/');

        if (!artifactStore.Exists(ContentPaths.LpBundle(product.Slug)))
        {
            await RenderAsync(product, baseUrl, ct);
        }

        product.SetLpUrl($"{baseUrl}/lp/{product.Slug}");

        if (product.Status == ProductStatus.Pipeline && product.Stage == ProductStage.Lp)
        {
            var requiresApproval = await settings.GetOrDefaultAsync(SettingKeys.PublishingRequiresApproval, true, ct);
            var transition = requiresApproval ? product.SubmitForApproval() : product.BeginPublishing();
            if (transition.IsFailure)
            {
                return transition;
            }

            logger.LogInformation("LP pronta para {Slug}; produto {Mode}",
                product.Slug, requiresApproval ? "aguardando aprovação" : "em publicação (Auto)");
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task RenderAsync(Product product, string baseUrl, CancellationToken ct)
    {
        var niche = await niches.GetByIdAsync(product.NicheId, ct);
        var nicheSlug = niche?.Slug ?? product.Slug;
        var palette = await ResolvePaletteAsync(nicheSlug, ct);

        var copy = await ReadCopyAsync(product.Slug, ct);
        var cover = await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug), ct);

        var checkoutUrl = $"{baseUrl}/go/{product.Slug}";
        var pixelUrl = $"{baseUrl}/px.gif?s={Uri.EscapeDataString(product.Slug)}";

        var model = LandingPageBuilder.BuildModel(product.Title, copy, cover, checkoutUrl, pixelUrl, palette);
        var template = LpTemplateSelector.ForNiche(nicheSlug);
        var html = LandingPageBuilder.Render(model, template);

        var stored = await artifactStore.WriteBytesAsync(
            ContentPaths.LpBundle(product.Slug), Encoding.UTF8.GetBytes(html), ct);

        if (await artifacts.GetLatestAsync(product.Id, ArtifactType.LpBundle, ct) is null)
        {
            var meta = JsonSerializer.Serialize(
                new { template = template.ToString(), bytes = stored.SizeBytes }, JsonOptions);
            artifacts.Add(Artifact.Create(
                product.Id, ArtifactType.LpBundle, stored.RelativePath, stored.Sha256, Version, meta, clock.UtcNow));
        }

        logger.LogInformation("LP renderizada para {Slug} (template {Template}, {Bytes} bytes, capa: {HasCover})",
            product.Slug, template, stored.SizeBytes, cover is not null);
    }

    private async Task<LpCopyDto?> ReadCopyAsync(string slug, CancellationToken ct)
    {
        var json = await fileStore.ReadTextAsync(ContentPaths.SalesCopy(slug), ct);
        if (json is null)
        {
            return null;
        }

        var parsed = AiJson.Parse<LpCopyDto>(json, "ebook.sales-copy");
        return parsed.IsSuccess ? parsed.Value : null;
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
}
