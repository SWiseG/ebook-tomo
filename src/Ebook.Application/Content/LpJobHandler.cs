using System.Globalization;
using System.Text;
using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Ai;
using Ebook.Application.Common.Settings;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Lp;
using Ebook.Application.Media;
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
    IMediaGateway mediaGateway,
    IPromptLibrary promptLibrary,
    IPaletteResolver paletteResolver,
    IBrandResolver brandResolver,
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
        var palette = await paletteResolver.ResolveAsync(product.Slug, nicheSlug, ct);

        var copy = await ReadCopyAsync(product.Slug, ct);
        var cover = await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug), ct);
        var mockup = await artifactStore.ReadBytesAsync(ContentPaths.Mockup(product.Slug), ct); // hero 3D

        var checkoutUrl = $"{baseUrl}/go/{product.Slug}";
        var pixelUrl = $"{baseUrl}/px.gif?s={Uri.EscapeDataString(product.Slug)}";
        var deadline = await ResolveOfferDeadlineAsync(ct);

        // URLs absolutas para canonical/OG só fazem sentido com baseUrl público; capa via /media (E08).
        var canonicalUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl}/lp/{product.Slug}";
        var coverImageUrl = !string.IsNullOrWhiteSpace(baseUrl) && cover is not null
            ? $"{baseUrl}/media/{ContentPaths.Cover(product.Slug)}"
            : null;

        var legal = await ResolveLegalAsync(ct);
        var disclaimer = NicheStyleCatalog.DisclaimerFor(NicheStyleCatalog.Classify(nicheSlug));
        var showcase = await EnsureShowcaseAsync(product, nicheSlug, ct);

        // docs/17 P2-9: com baseUrl público, serve mockup/hero via /media/ e NÃO embute base64 (LP leve).
        var hasBase = !string.IsNullOrWhiteSpace(baseUrl);
        var mockupUrl = hasBase && mockup is not null ? $"{baseUrl}/media/{ContentPaths.Mockup(product.Slug)}" : null;
        var showcaseUrl = hasBase && showcase is not null ? $"{baseUrl}/media/{ContentPaths.LpHero(product.Slug)}" : null;

        // docs/18 P3: com baseUrl público, a capa também vai por /media/ (CoverUrl é fallback do hero)
        // e NÃO é embutida em base64 — LP leve p/ mobile. Em dev (sem baseUrl), embute como antes.
        var model = LandingPageBuilder.BuildModel(
            product.Title, copy, hasBase ? null : cover, checkoutUrl, pixelUrl, palette, deadline, canonicalUrl, coverImageUrl,
            legal, disclaimer,
            showcaseImage: showcaseUrl is null ? showcase : null,
            mockupImage: mockupUrl is null ? mockup : null,
            mockupUrl: mockupUrl, showcaseUrl: showcaseUrl, coverUrl: coverImageUrl);
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

    // Ilustração de herói por IA (Media Gateway, free-first + cache). Re-entrante: reusa o artefato
    // se já existe. Best-effort: qualquer falha → null e a seção showcase é omitida.
    private async Task<byte[]?> EnsureShowcaseAsync(Product product, string nicheSlug, CancellationToken ct)
    {
        var path = ContentPaths.LpHero(product.Slug);
        var existing = await artifactStore.ReadBytesAsync(path, ct);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            var vars = new Dictionary<string, string>
            {
                ["title"] = product.Title,
                ["nicheSlug"] = nicheSlug,
            };
            var rendered = await promptLibrary.RenderAsync("media/lp-hero", vars, ct);
            var basePrompt = rendered.IsSuccess
                ? rendered.Value
                : $"premium aspirational landing page hero illustration about {nicheSlug}, no text, modern editorial, 2:1 banner";
            var brand = await brandResolver.ResolveAsync(product.Slug, nicheSlug, ct); // docs/15 Frente A
            var prompt = brand.Decorate(basePrompt);

            var result = await mediaGateway.GenerateAsync(
                new MediaBrief("lp-hero", prompt, nicheSlug, nicheSlug, 1024, 512), ct);
            if (result.IsFailure)
            {
                return null;
            }

            await artifactStore.WriteBytesAsync(path, result.Value.Bytes, ct);
            logger.LogInformation("Ilustração da LP gerada para {Slug} via {Provider}",
                product.Slug, result.Value.Provider);
            return result.Value.Bytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao gerar ilustração da LP para {Slug}; seguindo sem ela", product.Slug);
            return null;
        }
    }

    // Dados legais do rodapé (config). Null se nada configurado → rodapé mínimo.
    private async Task<LpLegalDto?> ResolveLegalAsync(CancellationToken ct)
    {
        var company = await settings.GetOrDefaultAsync(SettingKeys.LegalCompanyName, string.Empty, ct);
        var cnpj = await settings.GetOrDefaultAsync(SettingKeys.LegalCnpj, string.Empty, ct);
        var email = await settings.GetOrDefaultAsync(SettingKeys.LegalContactEmail, string.Empty, ct);
        var privacy = await settings.GetOrDefaultAsync(SettingKeys.LegalPrivacyUrl, string.Empty, ct);
        var terms = await settings.GetOrDefaultAsync(SettingKeys.LegalTermsUrl, string.Empty, ct);

        if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(cnpj) && string.IsNullOrWhiteSpace(email)
            && string.IsNullOrWhiteSpace(privacy) && string.IsNullOrWhiteSpace(terms))
        {
            return null;
        }

        return new LpLegalDto(Nz(company), Nz(cnpj), Nz(email), Nz(privacy), Nz(terms));

        static string? Nz(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }

    // Prazo da oferta para o contador: só se configurado (ISO-8601) e futuro. Vazio = sem contador.
    private async Task<DateTime?> ResolveOfferDeadlineAsync(CancellationToken ct)
    {
        var raw = await settings.GetOrDefaultAsync(SettingKeys.LpOfferDeadlineUtc, string.Empty, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            // sem prazo fixo: contador rolante de N horas, se configurado (docs/15)
            var hours = await settings.GetOrDefaultAsync(SettingKeys.LpDefaultOfferHours, 0, ct);
            return hours > 0 ? clock.UtcNow.AddHours(hours) : null;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            && parsed > clock.UtcNow
                ? parsed
                : null;
    }
}
