using Ebook.Application.Ai;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Settings;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Ebook.Application.Knowledge;
using Ebook.Application.Media;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content.Lp.Lab;

/// <summary>
/// Geração de TESTE de landing page a partir de um nicho (sem criar e-book). Orquestra
/// copy (com feedback/"memória" injetado) → capa → ilustração por IA → render, e devolve o
/// HTML auto-contido + um rastro ("caminho") do que foi usado. Nada é persistido como produto.
/// </summary>
public sealed record GenerateTestLpCommand(Guid NicheId, string? Feedback) : ICommand<GenerateTestLpResult>;

public sealed record GenerateTestLpResult(string Html, LpTraceDto Trace);

/// <summary>Um passo do caminho percorrido pela LP (estágio, quem fez, como, resultado).</summary>
public sealed record LpTraceStep(string Stage, string Actor, string Detail, string Result);

public sealed record LpTraceDto(
    string NicheName,
    string Category,
    string Template,
    string PaletteBackground,
    string PaletteAccent,
    string HeadingFont,
    string BodyFont,
    string Title,
    bool FeedbackUsed,
    IReadOnlyList<LpTraceStep> Steps);

/// <summary>Campos extras (além do <see cref="LpCopyDto"/>) que o prompt lp/lab-copy também emite.</summary>
internal sealed record LabExtraDto(string? Title, string? ImagePrompt);

public sealed class GenerateTestLpHandler(
    INicheRepository niches,
    IKnowledgeService knowledge,
    IAiGateway aiGateway,
    IImageComposer composer,
    IPhotoProvider photos,
    IMediaGateway mediaGateway,
    ISettingsStore settings,
    ILogger<GenerateTestLpHandler> logger) : ICommandHandler<GenerateTestLpCommand, GenerateTestLpResult>
{
    public async Task<Result<GenerateTestLpResult>> HandleAsync(GenerateTestLpCommand command, CancellationToken ct)
    {
        var steps = new List<LpTraceStep>();

        var niche = await niches.GetByIdAsync(command.NicheId, ct);
        if (niche is null)
        {
            return Result.Failure<GenerateTestLpResult>(new Error("lplab.nicheNotFound", "Nicho não encontrado."));
        }

        var category = NicheStyleCatalog.Classify(niche.Slug);
        var palette = PaletteCatalog.ForNiche(niche.Slug);
        var template = LpTemplateSelector.ForNiche(niche.Slug);
        steps.Add(new LpTraceStep("Nicho selecionado", "Operador (teste)", niche.Name, $"categoria {category}"));

        var pack = await knowledge.EnsurePackAsync(niche, QualityTier.Commercial, ct);
        if (pack.IsFailure)
        {
            return Result.Failure<GenerateTestLpResult>(pack.Error);
        }
        steps.Add(new LpTraceStep("Conhecimento", "KnowledgeService", "Pack de dores/desejos/objeções do nicho", "ok"));

        var feedbackUsed = !string.IsNullOrWhiteSpace(command.Feedback);
        var copyAi = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "lp.lab-copy",
            PromptTemplate: "lp/lab-copy",
            Variables: new Dictionary<string, string>
            {
                ["nicheName"] = niche.Name,
                ["knowledgePack"] = pack.Value,
                ["feedback"] = feedbackUsed ? command.Feedback! : "(nenhum)",
            },
            MaxOutputTokensEst: 2200), ct);
        if (copyAi.IsFailure)
        {
            return Result.Failure<GenerateTestLpResult>(copyAi.Error);
        }

        var copyParsed = AiJson.Parse<LpCopyDto>(copyAi.Value.Content, "lp.lab-copy");
        if (copyParsed.IsFailure)
        {
            return Result.Failure<GenerateTestLpResult>(copyParsed.Error);
        }
        var copy = copyParsed.Value;
        var extra = AiJson.Parse<LabExtraDto>(copyAi.Value.Content, "lp.lab-copy") is { IsSuccess: true } e ? e.Value : null;
        var title = string.IsNullOrWhiteSpace(extra?.Title) ? niche.Name : extra!.Title!;
        steps.Add(new LpTraceStep(
            "Copy de venda",
            $"Claude / {copyAi.Value.Provider}",
            "Prompt lp/lab-copy" + (feedbackUsed ? " + memória do avaliador" : string.Empty),
            copyAi.Value.CacheHit ? "cache" : $"gerado em {copyAi.Value.DurationMs}ms"));

        // Capa: composição SkiaSharp + foto de fundo (cadeia free-first via IPhotoProvider).
        var photo = await photos.TryGetBackgroundAsync(niche.Name, ct);
        var cover = composer.RenderCover(new CoverArt(title, copy.Subheadline, niche.Name, palette), photo);
        steps.Add(new LpTraceStep(
            "Capa", "SkiaSharp", photo is null ? "gradiente da paleta (sem foto)" : "com foto de fundo do gateway", "ok"));

        // Ilustração de herói por IA (prompt autoral do Claude → Media Gateway free-first).
        byte[]? hero = null;
        var heroPrompt = string.IsNullOrWhiteSpace(extra?.ImagePrompt)
            ? $"premium aspirational landing page hero illustration about {niche.Name}, modern editorial, no text, 2:1 banner"
            : extra!.ImagePrompt!;
        var heroResult = await mediaGateway.GenerateAsync(
            new MediaBrief("lp-hero", heroPrompt, niche.Slug, niche.Slug, 1024, 512), ct);
        if (heroResult.IsSuccess)
        {
            hero = heroResult.Value.Bytes;
            steps.Add(new LpTraceStep(
                "Ilustração de herói", $"Media Gateway / {heroResult.Value.Provider}", heroPrompt,
                heroResult.Value.CacheHit ? "cache" : "gerado"));
        }
        else
        {
            steps.Add(new LpTraceStep("Ilustração de herói", "Media Gateway", heroPrompt, "nenhum provedor — omitida"));
        }

        var legal = await ResolveLegalAsync(ct);
        var disclaimer = NicheStyleCatalog.DisclaimerFor(category);

        // URLs neutras para preview (sem checkout/pixel reais).
        var model = LandingPageBuilder.BuildModel(
            title, copy, cover, "#", "#", palette,
            offerDeadlineUtc: null, canonicalUrl: null, coverImageUrl: null,
            legal: legal, disclaimer: disclaimer, showcaseImage: hero);
        var html = LandingPageBuilder.Render(model, template);
        steps.Add(new LpTraceStep("Renderização", "LandingPageBuilder", $"Template {template} + paleta do nicho", $"{html.Length} bytes"));

        logger.LogInformation("LP de teste gerada para nicho {Niche} (template {Template}, feedback: {Fb})",
            niche.Slug, template, feedbackUsed);

        var trace = new LpTraceDto(
            niche.Name, category.ToString(), template.ToString(),
            palette.Background, palette.Accent, palette.HeadingFont, palette.BodyFont,
            title, feedbackUsed, steps);

        return Result.Success(new GenerateTestLpResult(html, trace));
    }

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
}
