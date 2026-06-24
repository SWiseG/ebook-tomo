using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Ebook.Application.Media;
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
    IMediaGateway mediaGateway,
    IPaletteResolver paletteResolver,
    IPaletteDirector paletteDirector,
    IBrandResolver brandResolver,
    IBrandDirector brandDirector,
    ICoverDirector coverDirector,
    ICoverQa coverQa,
    IPromptLibrary promptLibrary,
    ISettingsStore settings,
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

        var outline = await ContentPaths.ReadOutlineAsync(fileStore, product.Slug, ct);
        var title = outline.IsSuccess ? outline.Value.Title : product.Title;
        var subtitle = outline.IsSuccess ? outline.Value.Subtitle : null;
        var topics = outline.IsSuccess
            ? string.Join("; ", outline.Value.Chapters.Select(c => c.Title))
            : title;

        // Paleta por IA (docs/14 WP-2): gera+persiste a identidade do produto ANTES de resolver, para
        // que capa, PDF e LP (que rodam depois) leiam a MESMA paleta. Best-effort: falha → catálogo.
        await paletteDirector.EnsureAsync(product.Slug, nicheSlug, title, ct);
        await brandDirector.EnsureAsync(product.Slug, nicheSlug, title, ct); // docs/15 Frente A
        var palette = await paletteResolver.ResolveAsync(product.Slug, nicheSlug, ct);
        var brand = await brandResolver.ResolveAsync(product.Slug, nicheSlug, ct);

        // Diretor de capa por IA (docs/14 WP-4): eyebrow, benefícios, selo e a CENA do fundo.
        var plan = await coverDirector.PlanAsync(title, subtitle, nicheSlug, topics, ct);

        // Fundo: ilustração editorial da cena planejada (IA, free-first); sem cena/provedor, cai na
        // foto de banco por nome do nicho. Os scrims do RenderCover deixam a imagem visível.
        var background = await ResolveBackgroundAsync(plan, brand, palette, title, niche?.Name ?? product.Title, nicheSlug, product.Id, ct);

        var art = new CoverArt(
            title,
            string.IsNullOrWhiteSpace(plan?.Subtitle) ? subtitle : plan!.Subtitle,
            niche?.Name,
            palette,
            Eyebrow: plan?.Eyebrow,
            Features: plan?.Features.Select(f => new CoverFeature(f.Text, f.Icon)).ToList(),
            Seal: plan?.Seal);

        // Caminho full-AI (docs/14 WP-5), gated: a IA gera a capa INTEIRA com texto; QA de visão
        // (WP-8) confere o título e, se reprovar (ou estiver desligado), usa a composição Skia rica.
        var coverPng = await TryFullAiCoverAsync(title, plan, palette, nicheSlug, product.Id, ct)
            ?? composer.RenderCover(art, background);
        var coverStored = await artifactStore.WriteBytesAsync(ContentPaths.Cover(product.Slug), coverPng, ct);
        await AddArtifactIfNewAsync(product.Id, ArtifactType.Cover, coverStored, ct);

        var mockupPng = composer.RenderMockup(coverPng, palette);
        var mockupStored = await artifactStore.WriteBytesAsync(ContentPaths.Mockup(product.Slug), mockupPng, ct);
        await AddArtifactIfNewAsync(product.Id, ArtifactType.Mockup, mockupStored, ct);

        // Banner da vitrine Kiwify/Hotmart ~300×250 (docs/17 P1-7)
        var bannerPng = composer.RenderMarketplaceBanner(coverPng, palette);
        var bannerStored = await artifactStore.WriteBytesAsync(ContentPaths.Banner(product.Slug), bannerPng, ct);
        await AddArtifactIfNewAsync(product.Id, ArtifactType.Banner, bannerStored, ct);

        logger.LogInformation("Capa e mockup gerados para {Slug} (plano IA: {HasPlan}, fundo: {HasBg})",
            product.Slug, plan is not null, background is not null);
    }

    // Ilustração de fundo da capa: cena concreta planejada pela IA via Media Gateway (free-first),
    // com fallback para a foto de banco por nome do nicho. Best-effort: null → gradiente da paleta.
    private async Task<byte[]?> ResolveBackgroundAsync(
        Content.Images.CoverPlanDto? plan, ProductBrand brand, NichePalette palette, string title,
        string nicheName, string nicheSlug, Guid productId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(plan?.Scene))
        {
            // docs/15 Frente C: prompt amplo (cena+título+paleta+direção de arte) e roteamento
            // Illustration → generativos primeiro (Gemini é o 1º da cadeia). Fallback: foto de banco.
            var rendered = await promptLibrary.RenderAsync("media/cover-bg", new Dictionary<string, string>
            {
                ["title"] = title,
                ["niche"] = nicheSlug,
                ["scene"] = plan!.Scene,
                ["background"] = palette.Background,
                ["accent"] = palette.Accent,
            }, ct);
            var prompt = brand.Decorate(rendered.IsSuccess ? rendered.Value : plan.Scene);

            var media = await mediaGateway.GenerateAsync(
                new MediaBrief("cover-bg", prompt, nicheName, nicheSlug, 1600, 2400, productId, MediaKind.Illustration), ct);
            if (media.IsSuccess)
            {
                return media.Value.Bytes;
            }
        }

        return await photos.TryGetBackgroundAsync(nicheName, ct);
    }

    // Capa INTEIRA por IA (docs/14 WP-5): gated por setting; precisa do plano (eyebrow/features/cena).
    // Gera via modelo capaz de texto → QA de visão (WP-8) → aceita (normaliza p/ 1600×2400) ou null
    // (o chamador então usa a composição Skia). Best-effort: qualquer falha → null.
    private async Task<byte[]?> TryFullAiCoverAsync(
        string title, Content.Images.CoverPlanDto? plan, NichePalette palette, string nicheSlug, Guid productId, CancellationToken ct)
    {
        if (plan is null || !await settings.GetOrDefaultAsync(SettingKeys.CoverAiFullCover, false, ct))
        {
            return null;
        }

        var prompt = await promptLibrary.RenderAsync("media/cover-full", new Dictionary<string, string>
        {
            ["title"] = title,
            ["subtitle"] = plan.Subtitle,
            ["eyebrow"] = plan.Eyebrow,
            ["seal"] = plan.Seal,
            ["features"] = string.Join("; ", plan.Features.Select(f => f.Text)),
            ["scene"] = plan.Scene,
            ["background"] = palette.Background,
            ["accent"] = palette.Accent,
            ["onDark"] = palette.OnDark,
            ["displayFont"] = palette.Display,
        }, ct);
        if (prompt.IsFailure)
        {
            return null;
        }

        var media = await mediaGateway.GenerateAsync(
            new MediaBrief("cover-full", prompt.Value, nicheSlug, nicheSlug, 1600, 2400, productId, MediaKind.CoverWithText), ct);
        if (media.IsFailure)
        {
            return null;
        }

        var verdict = await coverQa.ReviewAsync(media.Value.Bytes, title, ct);
        if (!verdict.Accepted)
        {
            logger.LogInformation("Capa full-AI reprovada no QA ({Issues}); usando composição Skia.", verdict.Issues);
            return null;
        }

        logger.LogInformation("Capa full-AI aprovada (score {Score}) via {Provider}.", verdict.Score, media.Value.Provider);
        return composer.FitCover(media.Value.Bytes);
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
