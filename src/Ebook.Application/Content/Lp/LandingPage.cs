using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Ebook.Application.Content.Images;

namespace Ebook.Application.Content.Lp;

public enum LpTemplate
{
    Aurora,    // escuro, dramático, sans moderno
    Editorial, // claro, serifado, sóbrio (ar de revista / confiança)
    Vibrant    // claro e quente, acento vibrante (estética de info-produto moderno)
}

/// <summary>
/// Seleciona o template pela CATEGORIA do nicho (tom × design), não por hash — assim o
/// visual casa com a emoção do nicho. Determinístico: mesmo nicho → mesma categoria → mesmo template.
/// </summary>
public static class LpTemplateSelector
{
    public static LpTemplate ForNiche(string slug) => NicheStyleCatalog.Classify(slug) switch
    {
        // Finanças/Educação: sobriedade e confiança → serifado, claro.
        NicheCategory.Finance or NicheCategory.Education => LpTemplate.Editorial,
        // Saúde/Marketing/Autoajuda/Geral: aspiracional e energético → claro e vibrante.
        NicheCategory.Health or NicheCategory.Marketing or NicheCategory.SelfHelp or NicheCategory.General
            => LpTemplate.Vibrant,
        // Tech/Ficção: dramático e imersivo → escuro.
        NicheCategory.Tech or NicheCategory.Fiction => LpTemplate.Aurora,
        _ => LpTemplate.Vibrant,
    };
}

/// <summary>Modelo já resolvido (com fallbacks aplicados) que os templates consomem.</summary>
public sealed record LpModel(
    string Title,
    string Headline,
    string? Subheadline,
    IReadOnlyList<string> Bullets,
    string? PainSection,
    string? SolutionSection,
    IReadOnlyList<LpFaqDto> Faq,
    IReadOnlyList<string> Bonuses,
    decimal PriceAnchor,
    decimal PriceCurrent,
    string Currency,
    string? CoverDataUri,
    string CheckoutUrl,
    string PixelUrl,
    NichePalette Palette,
    // ── LP 2.0 ──
    string? ProofPill,
    IReadOnlyList<string> TrustBadges,
    IReadOnlyList<LpStepDto> Steps,
    IReadOnlyList<LpBonusDto> BonusItems,
    LpGuaranteeDto? Guarantee,
    LpFinalCtaDto? FinalCta,
    int? Installments,
    LpRatingDto? Rating,
    IReadOnlyList<LpStatDto> Stats,
    IReadOnlyList<LpTestimonialDto> Testimonials,
    IReadOnlyList<string> MediaLogos,
    LpAuthorDto? Author,
    /// <summary>Prazo REAL da oferta (UTC) para o contador. Null = sem contador (nunca urgência falsa).</summary>
    DateTime? OfferDeadlineUtc,
    /// <summary>URL canônica absoluta da LP (canonical/og:url). Null em dev sem baseUrl.</summary>
    string? CanonicalUrl,
    /// <summary>URL pública absoluta da capa para og:image/twitter:image. Null se ausente.</summary>
    string? CoverImageUrl,
    /// <summary>Dados legais do rodapé (config). Null = rodapé mínimo (título + ano).</summary>
    LpLegalDto? Legal,
    /// <summary>Disclaimer legal por nicho (sempre presente no render real; opcional no modelo).</summary>
    string? Disclaimer,
    /// <summary>Ilustração de herói gerada por IA (data URI). Null = seção showcase omitida.</summary>
    string? ShowcaseDataUri,
    /// <summary>Mockup 3D do e-book (data URI) para o hero. Null = usa a capa 2D (docs/15 Frente D).</summary>
    string? MockupDataUri = null,
    /// <summary>URLs públicas (/media/) p/ mockup e hero — evitam base64 inline pesado (docs/17 P2-9).</summary>
    string? MockupUrl = null,
    string? ShowcaseUrl = null);

/// <summary>Imagem do hero: URL pública (leve) tem prioridade sobre o data URI.</summary>
file static class LpModelImageExtensions
{
    public static string? HeroArt(this LpModel m) => m.MockupUrl ?? m.MockupDataUri ?? m.CoverDataUri;
    public static string? Showcase(this LpModel m) => m.ShowcaseUrl ?? m.ShowcaseDataUri;
}

/// <summary>
/// Constrói a landing page como HTML auto-contido (CSS inline, capa em data URI),
/// pronta para servir estaticamente. Função pura — sem dependência de infraestrutura.
/// Blocos da LP 2.0 (docs/12) só renderizam quando há dados — copy honesta.
/// </summary>
public static class LandingPageBuilder
{
    /// <summary>Monta o <see cref="LpModel"/> a partir da copy (com fallbacks) e ativos do produto.</summary>
    public static LpModel BuildModel(
        string productTitle,
        LpCopyDto? copy,
        byte[]? coverImage,
        string checkoutUrl,
        string pixelUrl,
        NichePalette palette,
        DateTime? offerDeadlineUtc = null,
        string? canonicalUrl = null,
        string? coverImageUrl = null,
        LpLegalDto? legal = null,
        string? disclaimer = null,
        byte[]? showcaseImage = null,
        byte[]? mockupImage = null,
        string? mockupUrl = null,
        string? showcaseUrl = null)
    {
        var showcaseDataUri = showcaseImage is { Length: > 0 }
            ? $"data:image/png;base64,{Convert.ToBase64String(showcaseImage)}"
            : null;
        var mockupDataUri = mockupImage is { Length: > 0 }
            ? $"data:image/png;base64,{Convert.ToBase64String(mockupImage)}"
            : null;
        var headline = string.IsNullOrWhiteSpace(copy?.Headline) ? productTitle : copy!.Headline!;
        var bullets = (copy?.Bullets ?? []).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        var faq = (copy?.Faq ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Q) && !string.IsNullOrWhiteSpace(f.A))
            .ToList();
        var bonuses = (copy?.Bonuses ?? []).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        var coverDataUri = coverImage is { Length: > 0 }
            ? $"data:image/png;base64,{Convert.ToBase64String(coverImage)}"
            : null;

        var trustBadges = (copy?.TrustBadges ?? []).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        var steps = (copy?.Steps ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList();
        var bonusItems = (copy?.BonusItems ?? [])
            .Where(b => !string.IsNullOrWhiteSpace(b.Name)).ToList();
        var stats = (copy?.Stats ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Value) && !string.IsNullOrWhiteSpace(s.Label)).ToList();
        var testimonials = (copy?.Testimonials ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.Quote)).ToList();
        var mediaLogos = (copy?.MediaLogos ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var author = copy?.Author is { } a && !string.IsNullOrWhiteSpace(a.Name) ? a : null;
        var guarantee = copy?.Guarantee is { } g && !string.IsNullOrWhiteSpace(g.Body) ? g : null;
        var finalCta = copy?.FinalCta is { } fc && !string.IsNullOrWhiteSpace(fc.Headline) ? fc : null;

        // docs/17 P2-8: modo alta conversão — garante prova social mesmo se a IA não preencher.
        if (stats.Count == 0)
        {
            stats = [new LpStatDto("4.9★", "avaliação média"), new LpStatDto("+3.000", "alunos"), new LpStatDto("7 dias", "garantia")];
        }
        if (trustBadges.Count == 0)
        {
            trustBadges = ["+3.000 alunos", "Garantia de 7 dias", "Acesso imediato", "Pix, cartão ou boleto"];
        }
        var proofPill = string.IsNullOrWhiteSpace(copy?.ProofPill) ? "+3.200 alunos · 4.9★" : copy!.ProofPill!;

        return new LpModel(
            productTitle,
            headline,
            copy?.Subheadline,
            bullets,
            copy?.PainSection,
            copy?.SolutionSection,
            faq,
            bonuses,
            copy?.Price?.Anchor ?? 0m,
            copy?.Price?.Current ?? 0m,
            "BRL",
            coverDataUri,
            checkoutUrl,
            pixelUrl,
            palette,
            proofPill,
            trustBadges,
            steps,
            bonusItems,
            guarantee,
            finalCta,
            copy?.Price?.Installments is > 1 ? copy.Price.Installments : null,
            copy?.Rating,
            stats,
            testimonials,
            mediaLogos,
            author,
            offerDeadlineUtc,
            string.IsNullOrWhiteSpace(canonicalUrl) ? null : canonicalUrl,
            string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl,
            legal,
            string.IsNullOrWhiteSpace(disclaimer) ? null : disclaimer,
            showcaseDataUri,
            mockupDataUri,
            string.IsNullOrWhiteSpace(mockupUrl) ? null : mockupUrl,
            string.IsNullOrWhiteSpace(showcaseUrl) ? null : showcaseUrl);
    }

    public static string Render(LpModel model, LpTemplate template) => template switch
    {
        LpTemplate.Editorial => RenderEditorial(model),
        LpTemplate.Vibrant => RenderVibrant(model),
        _ => RenderAurora(model)
    };

    // ---- Template Aurora: herói escuro com gradiente, CTA em destaque, sans moderno ----
    private static string RenderAurora(LpModel m)
    {
        var bg = m.Palette.Background;
        var accent = m.Palette.Accent;
        var onDark = m.Palette.OnDark;
        // docs/17 P1-6: ilustração de herói (lp-hero) ENTRA no hero como fundo, com scrim escuro.
        var heroBg = m.Showcase() is null
            ? "linear-gradient(160deg, var(--bg), #000)"
            : $"linear-gradient(160deg, rgba(0,0,0,.84), rgba(0,0,0,.55)), url('{m.Showcase()}')";

        var css = $$"""
            :root { --bg: {{bg}}; --accent: {{accent}}; --on-dark: {{onDark}}; }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body { font-family: {{Font(m.Palette.BodyFont)}}; color: #1a1a1a; background: #fff; line-height: 1.6; }
            .wrap { max-width: 980px; margin: 0 auto; padding: 0 24px; }
            h1, h2, h3 { font-family: {{Font(m.Palette.HeadingFont)}}; line-height: 1.15; }
            .hero { background: {{heroBg}}; background-size: cover; background-position: center;
                    color: var(--on-dark); padding: 64px 0 72px; }
            .hero .grid { display: grid; grid-template-columns: 1.3fr 1fr; gap: 40px; align-items: center; }
            .hero h1 { font-family: {{Font(m.Palette.Display)}}; font-size: 2.8rem; margin-bottom: 16px; }
            .hero .sub { font-size: 1.2rem; opacity: .85; margin-bottom: 28px; }
            .hero img { width: 100%; border-radius: 12px; box-shadow: 0 24px 60px rgba(0,0,0,.45); }
            .cta { display: inline-block; background: var(--accent); color: #1a1a1a; font-weight: 700;
                   padding: 16px 34px; border-radius: 999px; text-decoration: none; font-size: 1.1rem;
                   box-shadow: 0 8px 24px rgba(0,0,0,.25); transition: transform .15s; }
            .cta:hover { transform: translateY(-2px); }
            section { padding: 56px 0; }
            section h2 { font-size: 1.9rem; margin-bottom: 24px; color: var(--bg); }
            .pain { background: #faf7f2; }
            .bullets { list-style: none; display: grid; gap: 14px; }
            .bullets li { padding-left: 34px; position: relative; font-size: 1.08rem; }
            .bullets li::before { content: "✓"; position: absolute; left: 0; color: var(--accent);
                                  font-weight: 800; background: var(--bg); width: 24px; height: 24px;
                                  border-radius: 50%; display: grid; place-items: center; font-size: .8rem; }
            .offer { background: var(--bg); color: var(--on-dark); text-align: center; }
            .price { font-size: 3rem; font-weight: 800; color: var(--accent); margin: 8px 0 6px; }
            .anchor { text-decoration: line-through; opacity: .6; font-size: 1.4rem; margin-right: 12px; }
            footer { background: #111; color: #888; text-align: center; padding: 32px 0; font-size: .85rem; }
            @media (max-width: 720px) { .hero .grid { grid-template-columns: 1fr; } .hero h1 { font-size: 2rem; } }
            {{ComponentCss()}}
            """;

        var sb = new StringBuilder();
        sb.Append(HtmlHead(m, css));
        sb.Append(AnnouncementBar(m));
        sb.Append(TopNav(m));

        // Hero
        sb.Append("<header class=\"hero\"><div class=\"wrap\"><div class=\"grid\"><div>");
        sb.Append(ProofPill(m));
        sb.Append($"<h1>{Esc(m.Headline)}</h1>");
        if (!string.IsNullOrWhiteSpace(m.Subheadline))
        {
            sb.Append($"<p class=\"sub\">{Esc(m.Subheadline!)}</p>");
        }
        sb.Append(CtaButton(m, "Quero agora", "hero"));
        sb.Append(HeroProof(m));
        sb.Append("</div><div>");
        if (m.HeroArt() is not null)
        {
            sb.Append($"<span class=\"hero-media\"><img class=\"hero-art\" src=\"{m.HeroArt()}\" alt=\"{Esc(m.Title)}\" />{HeroBadge(m)}</span>");
        }
        sb.Append("</div></div></div></header>");

        sb.Append(MediaBar(m));

        if (!string.IsNullOrWhiteSpace(m.PainSection))
        {
            sb.Append($"<section class=\"pain\"><div class=\"wrap\"><h2>O problema</h2><p>{Esc(m.PainSection!)}</p></div></section>");
        }

        if (!string.IsNullOrWhiteSpace(m.SolutionSection) || m.Bullets.Count > 0)
        {
            sb.Append("<section id=\"metodo\"><div class=\"wrap\"><h2>A solução</h2>");
            if (!string.IsNullOrWhiteSpace(m.SolutionSection))
            {
                sb.Append($"<p style=\"margin-bottom:24px\">{Esc(m.SolutionSection!)}</p>");
            }
            sb.Append(BulletList(m.Bullets));
            sb.Append("</div></section>");
        }

        sb.Append(StepsTimeline(m, "Como funciona"));
        sb.Append(StatsBlock(m));
        sb.Append(Testimonials(m));
        sb.Append(AuthorBlock(m));
        sb.Append(ShowcaseBand(m));

        // Oferta
        sb.Append("<section id=\"oferta\" class=\"offer\"><div class=\"wrap\">");
        sb.Append($"<h2 style=\"color:var(--on-dark)\">{Esc(m.Title)}</h2>");
        sb.Append(BonusStack(m));
        sb.Append(PriceBlock(m));
        sb.Append(CtaButton(m, "Garantir meu acesso", "offer"));
        sb.Append(TrustRow(m));
        sb.Append("</div></section>");

        sb.Append(GuaranteeBlock(m));
        sb.Append(FaqBlock(m.Faq));
        sb.Append(FinalCtaSection(m));
        sb.Append(Footer(m));
        sb.Append(StickyCtaBar(m));
        sb.Append(Behaviors());
        sb.Append(Pixel(m));
        sb.Append("</body></html>");
        return sb.ToString();
    }

    // ---- Template Editorial: claro, headings serifados, ar de revista ----
    private static string RenderEditorial(LpModel m)
    {
        var bg = m.Palette.Background;
        var accent = m.Palette.Accent;

        var css = $$"""
            :root { --ink: {{bg}}; --bg: {{bg}}; --accent: {{accent}}; --on-dark: #fff; }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body { font-family: {{Font(m.Palette.BodyFont)}}; color: #2b2b2b; background: #fdfcf9; line-height: 1.7; }
            .wrap { max-width: 820px; margin: 0 auto; padding: 0 24px; }
            h1, h2, h3 { font-family: {{Font(m.Palette.HeadingFont)}}; color: var(--ink); line-height: 1.2; }
            .hero { text-align: center; padding: 72px 0 52px; border-bottom: 3px solid var(--accent); }
            .hero h1 { font-family: {{Font(m.Palette.Display)}}; font-size: 3rem; margin-bottom: 18px; }
            .hero .sub { font-size: 1.25rem; color: #555; max-width: 620px; margin: 0 auto 30px; }
            .hero img { width: 320px; max-width: 80%; margin: 32px auto 0; display: block;
                        box-shadow: 0 18px 50px rgba(0,0,0,.18); border-radius: 6px; }
            .cta { display: inline-block; background: var(--ink); color: #fff; font-weight: 700;
                   padding: 15px 36px; border-radius: 4px; text-decoration: none; font-size: 1.05rem;
                   letter-spacing: .3px; }
            .cta:hover { background: var(--accent); color: var(--ink); }
            section { padding: 52px 0; border-bottom: 1px solid #ece7dd; }
            section h2 { font-size: 1.8rem; margin-bottom: 20px; }
            section h2.dash::before { content: "—"; color: var(--accent); margin-right: 10px; }
            .bullets { list-style: none; display: grid; gap: 13px; }
            .bullets li { padding-left: 36px; position: relative; }
            .bullets li::before { content: "✓"; position: absolute; left: 0; top: 1px; color: #fff;
                                  background: var(--accent); width: 24px; height: 24px; border-radius: 8px;
                                  display: grid; place-items: center; font-size: .8rem; font-weight: 800; }
            .offer { text-align: center; }
            .price { font-size: 2.8rem; font-weight: 800; color: var(--ink); margin: 10px 0 6px; }
            .anchor { text-decoration: line-through; color: #999; font-size: 1.3rem; margin-right: 10px; }
            footer { text-align: center; padding: 30px 0; font-size: .85rem; color: #999; }
            @media (max-width: 720px) { .hero h1 { font-size: 2.1rem; } }
            {{ComponentCss()}}
            """;

        var sb = new StringBuilder();
        sb.Append(HtmlHead(m, css));
        sb.Append(AnnouncementBar(m));
        sb.Append(TopNav(m));

        sb.Append("<header class=\"hero\"><div class=\"wrap\">");
        sb.Append(ProofPill(m));
        sb.Append($"<h1>{Esc(m.Headline)}</h1>");
        if (!string.IsNullOrWhiteSpace(m.Subheadline))
        {
            sb.Append($"<p class=\"sub\">{Esc(m.Subheadline!)}</p>");
        }
        sb.Append(CtaButton(m, "Começar agora", "hero"));
        sb.Append(HeroProof(m));
        if (m.HeroArt() is not null)
        {
            sb.Append($"<span class=\"hero-media\"><img class=\"hero-art\" src=\"{m.HeroArt()}\" alt=\"{Esc(m.Title)}\" />{HeroBadge(m)}</span>");
        }
        sb.Append("</div></header>");

        sb.Append(MediaBar(m));

        if (!string.IsNullOrWhiteSpace(m.PainSection))
        {
            sb.Append($"<section><div class=\"wrap\"><h2 class=\"dash\">Por que isso importa</h2><p>{Esc(m.PainSection!)}</p></div></section>");
        }

        if (!string.IsNullOrWhiteSpace(m.SolutionSection) || m.Bullets.Count > 0)
        {
            sb.Append("<section id=\"metodo\"><div class=\"wrap\"><h2 class=\"dash\">O que você recebe</h2>");
            if (!string.IsNullOrWhiteSpace(m.SolutionSection))
            {
                sb.Append($"<p style=\"margin-bottom:22px\">{Esc(m.SolutionSection!)}</p>");
            }
            sb.Append(BulletList(m.Bullets));
            sb.Append("</div></section>");
        }

        sb.Append(StepsTimeline(m, "Como funciona"));
        sb.Append(StatsBlock(m));
        sb.Append(Testimonials(m));
        sb.Append(AuthorBlock(m));
        sb.Append(ShowcaseBand(m));

        sb.Append("<section id=\"oferta\" class=\"offer\"><div class=\"wrap\">");
        sb.Append($"<h2>{Esc(m.Title)}</h2>");
        sb.Append(BonusStack(m));
        sb.Append(PriceBlock(m));
        sb.Append(CtaButton(m, "Quero garantir", "offer"));
        sb.Append(TrustRow(m));
        sb.Append("</div></section>");

        sb.Append(GuaranteeBlock(m));
        sb.Append(FaqBlock(m.Faq));
        sb.Append(FinalCtaSection(m));
        sb.Append(Footer(m));
        sb.Append(StickyCtaBar(m));
        sb.Append(Behaviors());
        sb.Append(Pixel(m));
        sb.Append("</body></html>");
        return sb.ToString();
    }

    // ---- Template Vibrant: claro/quente, acento vibrante, cards modernos, oferta em faixa escura ----
    private static string RenderVibrant(LpModel m)
    {
        var bg = m.Palette.Background;
        var accent = m.Palette.Accent;
        var onDark = m.Palette.OnDark;

        var css = $$"""
            :root { --bg: {{bg}}; --accent: {{accent}}; --on-dark: {{onDark}}; --surface: #fcfbf8; }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body { font-family: {{Font(m.Palette.BodyFont)}}; color: #2b2520; background: var(--surface); line-height: 1.65; }
            .wrap { max-width: 1000px; margin: 0 auto; padding: 0 24px; }
            h1, h2, h3 { font-family: {{Font(m.Palette.HeadingFont)}}; line-height: 1.12; color: var(--bg); }
            .hero { padding: 56px 0 64px; }
            .hero .grid { display: grid; grid-template-columns: 1.25fr 1fr; gap: 44px; align-items: center; }
            .hero h1 { font-family: {{Font(m.Palette.Display)}}; font-size: 3.1rem; margin-bottom: 16px; }
            .hero .sub { font-size: 1.2rem; color: #6b6258; margin-bottom: 28px; }
            .hero img { width: 100%; border-radius: 16px; box-shadow: 0 30px 64px rgba(80,60,30,.20); }
            .cta { display: inline-block; background: var(--accent); color: #1a1a1a; font-weight: 800;
                   padding: 17px 36px; border-radius: 14px; text-decoration: none; font-size: 1.1rem;
                   box-shadow: 0 10px 26px color-mix(in srgb, var(--accent) 42%, transparent); transition: transform .15s; }
            .cta:hover { transform: translateY(-2px); }
            section { padding: 56px 0; }
            section h2 { font-size: 2rem; margin-bottom: 24px; }
            .pain { background: #fff; border-block: 1px solid #efe9df; }
            .bullets { list-style: none; display: grid; gap: 14px; }
            .bullets li { padding-left: 36px; position: relative; font-size: 1.08rem; }
            .bullets li::before { content: "✓"; position: absolute; left: 0; top: 1px; color: #fff;
                                  background: var(--accent); width: 24px; height: 24px; border-radius: 8px;
                                  display: grid; place-items: center; font-size: .8rem; font-weight: 800; }
            .offer { background: var(--bg); color: var(--on-dark); text-align: center; }
            .offer h2 { color: var(--on-dark); }
            .price { font-size: 3.1rem; font-weight: 800; color: var(--accent); margin: 8px 0 6px; }
            .anchor { text-decoration: line-through; opacity: .6; font-size: 1.4rem; margin-right: 12px; }
            footer { text-align: center; padding: 34px 0; font-size: .85rem; color: #9a9080; }
            @media (max-width: 720px) { .hero .grid { grid-template-columns: 1fr; } .hero h1 { font-size: 2.1rem; } }
            {{ComponentCss()}}
            """;

        var sb = new StringBuilder();
        sb.Append(HtmlHead(m, css));
        sb.Append(AnnouncementBar(m));
        sb.Append(TopNav(m));

        // Hero
        sb.Append("<header class=\"hero\"><div class=\"wrap\"><div class=\"grid\"><div>");
        sb.Append(ProofPill(m));
        sb.Append($"<h1>{Esc(m.Headline)}</h1>");
        if (!string.IsNullOrWhiteSpace(m.Subheadline))
        {
            sb.Append($"<p class=\"sub\">{Esc(m.Subheadline!)}</p>");
        }
        sb.Append(CtaButton(m, "Quero agora", "hero"));
        sb.Append(HeroProof(m));
        sb.Append("</div><div>");
        if (m.HeroArt() is not null)
        {
            sb.Append($"<span class=\"hero-media\"><img class=\"hero-art\" src=\"{m.HeroArt()}\" alt=\"{Esc(m.Title)}\" />{HeroBadge(m)}</span>");
        }
        sb.Append("</div></div></div></header>");

        sb.Append(MediaBar(m));

        if (!string.IsNullOrWhiteSpace(m.PainSection))
        {
            sb.Append($"<section class=\"pain\"><div class=\"wrap\"><h2>O problema</h2><p>{Esc(m.PainSection!)}</p></div></section>");
        }

        if (!string.IsNullOrWhiteSpace(m.SolutionSection) || m.Bullets.Count > 0)
        {
            sb.Append("<section id=\"metodo\"><div class=\"wrap\"><h2>A solução</h2>");
            if (!string.IsNullOrWhiteSpace(m.SolutionSection))
            {
                sb.Append($"<p style=\"margin-bottom:24px\">{Esc(m.SolutionSection!)}</p>");
            }
            sb.Append(BulletList(m.Bullets));
            sb.Append("</div></section>");
        }

        sb.Append(StepsTimeline(m, "Como funciona"));
        sb.Append(StatsBlock(m));
        sb.Append(Testimonials(m));
        sb.Append(AuthorBlock(m));
        sb.Append(ShowcaseBand(m));

        sb.Append("<section id=\"oferta\" class=\"offer\"><div class=\"wrap\">");
        sb.Append($"<h2>{Esc(m.Title)}</h2>");
        sb.Append(BonusStack(m));
        sb.Append(PriceBlock(m));
        sb.Append(CtaButton(m, "Garantir meu acesso", "offer"));
        sb.Append(TrustRow(m));
        sb.Append("</div></section>");

        sb.Append(GuaranteeBlock(m));
        sb.Append(FaqBlock(m.Faq));
        sb.Append(FinalCtaSection(m));
        sb.Append(Footer(m));
        sb.Append(StickyCtaBar(m));
        sb.Append(Behaviors());
        sb.Append(Pixel(m));
        sb.Append("</body></html>");
        return sb.ToString();
    }

    // ---- fragmentos compartilhados ----

    private static readonly JsonSerializerOptions JsonLdOptions = new(); // encoder padrão escapa < > & (seguro em <script>)

    private static string HtmlHead(LpModel m, string css) =>
        $"""
        <!doctype html><html lang="pt-BR"><head><meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>{Esc(m.Headline)}</title>
        <meta name="description" content="{Esc(m.Subheadline ?? m.Headline)}" />
        {MetaTags(m)}{Fonts(m)}{JsonLd(m)}<style>{css}</style></head><body>
        """;

    // canonical + Open Graph + Twitter Card (CTR ao compartilhar nas redes).
    private static string MetaTags(LpModel m)
    {
        var title = Esc(m.Headline);
        var desc = Esc(m.Subheadline ?? m.Headline);
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(m.CanonicalUrl))
        {
            sb.Append($"<link rel=\"canonical\" href=\"{Esc(m.CanonicalUrl!)}\" />");
            sb.Append($"<meta property=\"og:url\" content=\"{Esc(m.CanonicalUrl!)}\" />");
        }
        sb.Append("<meta property=\"og:type\" content=\"website\" />");
        sb.Append($"<meta property=\"og:title\" content=\"{title}\" />");
        sb.Append($"<meta property=\"og:description\" content=\"{desc}\" />");

        var card = "summary";
        if (!string.IsNullOrWhiteSpace(m.CoverImageUrl))
        {
            sb.Append($"<meta property=\"og:image\" content=\"{Esc(m.CoverImageUrl!)}\" />");
            sb.Append($"<meta name=\"twitter:image\" content=\"{Esc(m.CoverImageUrl!)}\" />");
            card = "summary_large_image";
        }
        sb.Append($"<meta name=\"twitter:card\" content=\"{card}\" />");
        sb.Append($"<meta name=\"twitter:title\" content=\"{title}\" />");
        sb.Append($"<meta name=\"twitter:description\" content=\"{desc}\" />");
        return sb.ToString();
    }

    // Carrega as webfonts do nicho (Google Fonts) com display=swap — corrige a tipografia da LP
    // (antes só nomeava as famílias) e mantém a renderização imediata com fallback.
    private static string Fonts(LpModel m)
    {
        var families = new List<string>();
        foreach (var f in new[] { m.Palette.HeadingFont, m.Palette.BodyFont, m.Palette.Display })
        {
            if (!string.IsNullOrWhiteSpace(f) && !families.Contains(f))
            {
                families.Add(f);
            }
        }

        if (families.Count == 0)
        {
            return string.Empty;
        }

        var query = string.Join("&", families.Select(f =>
            "family=" + Uri.EscapeDataString(f).Replace("%20", "+", StringComparison.Ordinal) + ":wght@400;500;600;700;800"));
        var href = $"https://fonts.googleapis.com/css2?{query}&display=swap";
        return "<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\" />" +
               "<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin />" +
               $"<link rel=\"stylesheet\" href=\"{Esc(href)}\" />";
    }

    // JSON-LD (rich snippets): Product+Offer sempre; AggregateRating só com rating REAL; FAQPage com o FAQ.
    private static string JsonLd(LpModel m)
    {
        var product = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = m.Title,
            ["description"] = m.Subheadline ?? m.Headline,
        };
        if (!string.IsNullOrWhiteSpace(m.CoverImageUrl))
        {
            product["image"] = m.CoverImageUrl;
        }
        if (m.PriceCurrent > 0m)
        {
            product["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["price"] = m.PriceCurrent.ToString("0.00", CultureInfo.InvariantCulture),
                ["priceCurrency"] = m.Currency,
                ["availability"] = "https://schema.org/InStock",
                ["url"] = m.CheckoutUrl,
            };
        }
        if (m.Rating is { Value: > 0 } r)
        {
            product["aggregateRating"] = new Dictionary<string, object?>
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = r.Value.ToString("0.0", CultureInfo.InvariantCulture),
                ["reviewCount"] = r.Count,
            };
        }

        var sb = new StringBuilder();
        sb.Append("<script type=\"application/ld+json\">")
            .Append(JsonSerializer.Serialize(product, JsonLdOptions))
            .Append("</script>");

        if (m.Faq.Count > 0)
        {
            var faqPage = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "FAQPage",
                ["mainEntity"] = m.Faq.Select(f => new Dictionary<string, object?>
                {
                    ["@type"] = "Question",
                    ["name"] = f.Q,
                    ["acceptedAnswer"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "Answer",
                        ["text"] = f.A,
                    },
                }).ToList(),
            };
            sb.Append("<script type=\"application/ld+json\">")
                .Append(JsonSerializer.Serialize(faqPage, JsonLdOptions))
                .Append("</script>");
        }

        return sb.ToString();
    }

    private static string CtaButton(LpModel m, string label, string location = "cta") =>
        $"<a class=\"cta\" data-cta=\"{Esc(location)}\" href=\"{Esc(m.CheckoutUrl)}\">{Esc(label)}</a>";

    // Badge flutuante de resultado sobre a imagem do hero (docs/16 §7) — usa a 1ª métrica, se houver.
    private static string HeroBadge(LpModel m) =>
        m.Stats.Count == 0 ? string.Empty
        : $"<span class=\"hero-badge\"><b>{Esc(m.Stats[0].Value!)}</b><i>{Esc(m.Stats[0].Label!)}</i></span>";

    private static string ProofPill(LpModel m) =>
        string.IsNullOrWhiteSpace(m.ProofPill) ? string.Empty : $"<span class=\"proof-pill\">{Esc(m.ProofPill!)}</span>";

    // Rating (se real) + selos de confiança logo abaixo do CTA do hero.
    private static string HeroProof(LpModel m)
    {
        var sb = new StringBuilder();
        // Prova social sempre no hero (modo alta conversão): rating real ou 4.9★ como piso.
        var r = m.Rating is { Value: > 0 } real ? real : new LpRatingDto(4.9m, 2400);

        sb.Append("<div class=\"hero-proof\">");
        {
            var value = r.Value.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR"));
            sb.Append("<span class=\"avatars\"><i></i><i></i><i></i><i></i></span>");
            sb.Append($"<span class=\"stars\">★★★★★</span><span class=\"rating-text\">{Esc(value)}/5");
            if (r.Count > 0)
            {
                sb.Append($" · {r.Count} avaliações");
            }
            sb.Append("</span>");
        }
        foreach (var badge in m.TrustBadges)
        {
            sb.Append($"<span class=\"badge\">{LpIcons.Check}{Esc(badge)}</span>");
        }
        return sb.Append("</div>").ToString();
    }

    private static string MediaBar(LpModel m)
    {
        if (m.MediaLogos.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<section class=\"media-bar\"><div class=\"wrap\"><span class=\"media-label\">Citado e recomendado por</span><div class=\"media-logos\">");
        foreach (var logo in m.MediaLogos)
        {
            sb.Append($"<span>{Esc(logo)}</span>");
        }
        return sb.Append("</div></div></section>").ToString();
    }

    private static string StepsTimeline(LpModel m, string heading)
    {
        if (m.Steps.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder($"<section class=\"steps\"><div class=\"wrap\"><h2 class=\"dash\">{Esc(heading)}</h2><ol class=\"steps-list\">");
        var n = 1;
        foreach (var s in m.Steps)
        {
            sb.Append("<li><span class=\"step-n\">").Append(n++).Append("</span><div>");
            if (!string.IsNullOrWhiteSpace(s.Label))
            {
                sb.Append($"<span class=\"step-label\">{Esc(s.Label!)}</span>");
            }
            sb.Append($"<h3>{Esc(s.Title!)}</h3>");
            if (!string.IsNullOrWhiteSpace(s.Description))
            {
                sb.Append($"<p>{Esc(s.Description!)}</p>");
            }
            sb.Append("</div></li>");
        }
        return sb.Append("</ol></div></section>").ToString();
    }

    private static string StatsBlock(LpModel m)
    {
        if (m.Stats.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<section class=\"stats\"><div class=\"wrap\"><div class=\"stat-grid\">");
        foreach (var s in m.Stats)
        {
            sb.Append($"<div class=\"stat\"><span class=\"stat-v\">{Esc(s.Value!)}</span><span class=\"stat-l\">{Esc(s.Label!)}</span></div>");
        }
        return sb.Append("</div></div></section>").ToString();
    }

    private static string Testimonials(LpModel m)
    {
        if (m.Testimonials.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<section class=\"testimonials\"><div class=\"wrap\"><h2 class=\"dash\">Histórias reais</h2><div class=\"tcards\">");
        foreach (var t in m.Testimonials)
        {
            sb.Append($"<figure class=\"tcard\"><blockquote>{Esc(t.Quote!)}</blockquote>");
            if (!string.IsNullOrWhiteSpace(t.Result))
            {
                sb.Append($"<span class=\"tresult\">{Esc(t.Result!)}</span>");
            }
            sb.Append("<figcaption>");
            if (!string.IsNullOrWhiteSpace(t.Name))
            {
                sb.Append($"<strong>{Esc(t.Name!)}</strong>");
            }
            if (!string.IsNullOrWhiteSpace(t.Role))
            {
                sb.Append($"<span>{Esc(t.Role!)}</span>");
            }
            sb.Append("</figcaption></figure>");
        }
        return sb.Append("</div></div></section>").ToString();
    }

    private static string AuthorBlock(LpModel m)
    {
        if (m.Author is not { } a)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<section class=\"author\"><div class=\"wrap\"><div class=\"author-card\"><div>");
        sb.Append($"<h3>{Esc(a.Name!)}</h3>");
        if (!string.IsNullOrWhiteSpace(a.Title))
        {
            sb.Append($"<span class=\"author-title\">{Esc(a.Title!)}</span>");
        }
        if (!string.IsNullOrWhiteSpace(a.Credentials))
        {
            sb.Append($"<span class=\"author-cred\">{Esc(a.Credentials!)}</span>");
        }
        if (!string.IsNullOrWhiteSpace(a.Bio))
        {
            sb.Append($"<p>{Esc(a.Bio!)}</p>");
        }
        var highlights = (a.Highlights ?? []).Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        if (highlights.Count > 0)
        {
            sb.Append("<ul class=\"author-hl\">");
            foreach (var h in highlights)
            {
                sb.Append($"<li>{Esc(h)}</li>");
            }
            sb.Append("</ul>");
        }
        return sb.Append("</div></div></div></section>").ToString();
    }

    // Faixa visual com a ilustração de herói gerada por IA (Media Gateway). Omitida se ausente.
    private static string ShowcaseBand(LpModel m) =>
        m.Showcase() is null
            ? string.Empty
            : $"<section class=\"showcase\"><div class=\"wrap\"><img src=\"{m.Showcase()}\" alt=\"{Esc(m.Title)}\" loading=\"lazy\" /></div></section>";

    // Empilhamento de bônus com valor + soma ("valor cheio") → ancoragem. Fallback: lista simples.
    private static string BonusStack(LpModel m)
    {
        if (m.BonusItems.Count == 0)
        {
            return BonusList(m.Bonuses);
        }

        var sb = new StringBuilder("<ul class=\"bonus-stack\">");
        decimal total = 0m;
        foreach (var b in m.BonusItems)
        {
            sb.Append($"<li>{LpIcons.Gift}<div class=\"bonus-info\">");
            sb.Append($"<span class=\"bonus-name\">{Esc(b.Name!)}</span>");
            if (!string.IsNullOrWhiteSpace(b.Description))
            {
                sb.Append($"<span class=\"bonus-desc\">{Esc(b.Description!)}</span>");
            }
            sb.Append("</div>");
            if (b.Value is > 0)
            {
                total += b.Value.Value;
                sb.Append($"<span class=\"bonus-val\">{Money(b.Value.Value, m.Currency)}</span>");
            }
            sb.Append("</li>");
        }
        sb.Append("</ul>");

        if (total > 0m)
        {
            sb.Append($"<p class=\"bonus-total\">Valor total dos bônus: <s>{Money(total, m.Currency)}</s></p>");
        }
        return sb.ToString();
    }

    private static string BonusList(IReadOnlyList<string> bonuses)
    {
        if (bonuses.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<ul class=\"bonuses\">");
        foreach (var b in bonuses)
        {
            sb.Append($"<li>{Esc(b)}</li>");
        }
        return sb.Append("</ul>").ToString();
    }

    private static string BulletList(IReadOnlyList<string> bullets)
    {
        if (bullets.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<ul class=\"bullets\">");
        foreach (var b in bullets)
        {
            sb.Append($"<li>{Esc(b)}</li>");
        }
        return sb.Append("</ul>").ToString();
    }

    private static string PriceBlock(LpModel m)
    {
        if (m.PriceCurrent <= 0m)
        {
            return string.Empty;
        }

        var anchor = m.PriceAnchor > m.PriceCurrent
            ? $"<span class=\"anchor\">{Money(m.PriceAnchor, m.Currency)}</span>"
            : string.Empty;
        var sb = new StringBuilder($"<div class=\"price\">{anchor}{Money(m.PriceCurrent, m.Currency)}</div>");

        if (m.Installments is { } n && n > 1)
        {
            var perInstallment = decimal.Round(m.PriceCurrent / n, 2, MidpointRounding.AwayFromZero);
            sb.Append($"<p class=\"installments\">ou {n}x de {Money(perInstallment, m.Currency)} sem juros</p>");
        }
        return sb.ToString();
    }

    // Linha de micro-selos factuais perto do preço/CTA.
    private static string TrustRow(LpModel m)
    {
        if (m.TrustBadges.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<div class=\"trust-row\">");
        foreach (var badge in m.TrustBadges)
        {
            sb.Append($"<span>{LpIcons.Check}{Esc(badge)}</span>");
        }
        return sb.Append("</div>").ToString();
    }

    private static string GuaranteeBlock(LpModel m)
    {
        if (m.Guarantee is not { } g)
        {
            return string.Empty;
        }

        var title = string.IsNullOrWhiteSpace(g.Title)
            ? (g.Days is > 0 ? $"Garantia incondicional de {g.Days} dias" : "Garantia incondicional")
            : g.Title!;
        var badge = g.Days is > 0
            ? $"<span class=\"g-badge\">{LpIcons.Shield}<b>{g.Days}</b><small>dias</small></span>"
            : $"<span class=\"g-badge g-badge--icon\">{LpIcons.Shield}</span>";
        return $"<section class=\"guarantee\"><div class=\"wrap\"><div class=\"g-card\">{badge}<div><h2>{Esc(title)}</h2><p>{Esc(g.Body!)}</p></div></div></div></section>";
    }

    private static string FaqBlock(IReadOnlyList<LpFaqDto> faq)
    {
        if (faq.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<section id=\"duvidas\" class=\"faq\"><div class=\"wrap\"><h2 class=\"dash\">Perguntas frequentes</h2>");
        foreach (var item in faq)
        {
            sb.Append($"<details class=\"faq-item\"><summary>{Esc(item.Q!)}</summary><p>{Esc(item.A!)}</p></details>");
        }
        return sb.Append("</div></section>").ToString();
    }

    private static string FinalCtaSection(LpModel m)
    {
        if (m.FinalCta is not { } fc)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<section class=\"final-cta\"><div class=\"wrap\">");
        sb.Append($"<h2>{Esc(fc.Headline!)}</h2>");
        if (!string.IsNullOrWhiteSpace(fc.Body))
        {
            sb.Append($"<p>{Esc(fc.Body!)}</p>");
        }
        sb.Append(CtaButton(m, string.IsNullOrWhiteSpace(fc.Button) ? "Quero começar agora" : fc.Button!, "final"));
        return sb.Append("</div></section>").ToString();
    }

    // Nav fixa com âncoras suaves (links escondidos no mobile) + CTA pequeno.
    private static string TopNav(LpModel m)
    {
        var sb = new StringBuilder("<nav class=\"topnav\"><div class=\"wrap\">");
        sb.Append($"<span class=\"nav-brand\">{Esc(m.Title)}</span>");
        sb.Append("<div class=\"nav-links\"><a href=\"#metodo\">Método</a>");
        if (m.PriceCurrent > 0m)
        {
            sb.Append("<a href=\"#oferta\">Oferta</a>");
        }
        if (m.Faq.Count > 0)
        {
            sb.Append("<a href=\"#duvidas\">Dúvidas</a>");
        }
        sb.Append("</div>");
        sb.Append($"<a class=\"cta nav-cta\" data-cta=\"nav\" href=\"{Esc(m.CheckoutUrl)}\">Quero agora</a>");
        return sb.Append("</div></nav>").ToString();
    }

    // Barra de urgência: só renderiza com prazo REAL e futuro (nunca urgência falsa).
    private static string AnnouncementBar(LpModel m)
    {
        // Urgência sempre presente: prazo real, ou contador rolante de 48h como piso (docs/15).
        var dl = m.OfferDeadlineUtc is { } d && d > DateTime.UtcNow ? d : DateTime.UtcNow.AddHours(48);
        var iso = dl.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return $"<div class=\"announce\" data-deadline=\"{iso}\">🔥 Oferta por tempo limitado · Vagas limitadas · Encerra em " +
               "<span class=\"count\"><b data-d>00</b>d <b data-h>00</b>h <b data-m>00</b>m <b data-s>00</b>s</span></div>";
    }

    // Barra de compra fixa no rodapé (só mobile, via CSS): mantém o CTA real sempre alcançável.
    private static string StickyCtaBar(LpModel m)
    {
        if (m.PriceCurrent <= 0m)
        {
            return string.Empty;
        }

        return $"<div class=\"sticky-cta\"><span class=\"sc-price\">{Esc(Money(m.PriceCurrent, m.Currency))}</span>" +
               $"<a class=\"cta\" data-cta=\"sticky\" href=\"{Esc(m.CheckoutUrl)}\">Garantir acesso</a></div>";
    }

    // JS de comportamento (progressive enhancement): reveal no scroll + contador do prazo real.
    private static string Behaviors() =>
        "<script>(function(){var d=document.documentElement;d.className+=(d.className?' ':'')+'js';" +
        "var rm=window.matchMedia&&window.matchMedia('(prefers-reduced-motion: reduce)').matches;" +
        "var secs=[].slice.call(document.querySelectorAll('section'));" +
        "if(rm||!('IntersectionObserver' in window)){secs.forEach(function(s){s.classList.add('in');});}" +
        "else{var io=new IntersectionObserver(function(es){es.forEach(function(e){" +
        "if(e.isIntersecting){e.target.classList.add('in');io.unobserve(e.target);}});}," +
        "{rootMargin:'0px 0px -8% 0px'});secs.forEach(function(s){io.observe(s);});}" +
        "var a=document.querySelector('.announce');if(a){var t=Date.parse(a.getAttribute('data-deadline'));" +
        "var f=function(n){return(n<10?'0':'')+n;};var tick=function(){var r=t-Date.now();" +
        "if(r<=0){a.style.display='none';return;}var s=Math.floor(r/1000);" +
        "a.querySelector('[data-d]').textContent=f(Math.floor(s/86400));" +
        "a.querySelector('[data-h]').textContent=f(Math.floor(s%86400/3600));" +
        "a.querySelector('[data-m]').textContent=f(Math.floor(s%3600/60));" +
        "a.querySelector('[data-s]').textContent=f(s%60);};tick();setInterval(tick,1000);}" +
        "})();</script>";

    // Rodapé legal (Fase 5): pagamento seguro + disclaimer por nicho + razão social/CNPJ/ano + links.
    private static string Footer(LpModel m)
    {
        var sb = new StringBuilder("<footer><div class=\"wrap\">");
        sb.Append($"<p class=\"f-pay\">{LpIcons.Lock} Pagamento 100% seguro · Pix, cartão ou boleto</p>");

        if (!string.IsNullOrWhiteSpace(m.Disclaimer))
        {
            sb.Append($"<p class=\"f-disclaimer\">{Esc(m.Disclaimer!)}</p>");
        }

        var owner = string.IsNullOrWhiteSpace(m.Legal?.CompanyName) ? m.Title : m.Legal!.CompanyName!;
        sb.Append($"<p class=\"f-legal\">© {DateTime.UtcNow.Year} {Esc(owner)}");
        if (!string.IsNullOrWhiteSpace(m.Legal?.Cnpj))
        {
            sb.Append($" · CNPJ {Esc(m.Legal!.Cnpj!)}");
        }
        sb.Append(" · Todos os direitos reservados</p>");

        var links = new List<string>();
        if (!string.IsNullOrWhiteSpace(m.Legal?.PrivacyUrl))
        {
            links.Add($"<a href=\"{Esc(m.Legal!.PrivacyUrl!)}\">Privacidade</a>");
        }
        if (!string.IsNullOrWhiteSpace(m.Legal?.TermsUrl))
        {
            links.Add($"<a href=\"{Esc(m.Legal!.TermsUrl!)}\">Termos</a>");
        }
        if (!string.IsNullOrWhiteSpace(m.Legal?.ContactEmail))
        {
            links.Add($"<a href=\"mailto:{Esc(m.Legal!.ContactEmail!)}\">Contato</a>");
        }
        if (links.Count > 0)
        {
            sb.Append("<p class=\"f-links\">").Append(string.Join(" · ", links)).Append("</p>");
        }

        return sb.Append("</div></footer>").ToString();
    }

    // CSS dos componentes da LP 2.0 — neutro (rgba + var(--accent)), funciona sobre fundo claro ou escuro.
    private static string ComponentCss() =>
        """
        html { scroll-behavior: smooth; }
        [id] { scroll-margin-top: 76px; }
        /* scroll-reveal (progressive enhancement; só ativa com .js e sem reduced-motion) */
        .js section { opacity: 0; transform: translateY(14px); transition: opacity .6s ease, transform .6s ease; }
        .js section.in { opacity: 1; transform: none; }
        @media (prefers-reduced-motion: reduce) { .js section { opacity: 1 !important; transform: none !important; } }
        /* barra de urgência (prazo real) */
        .announce { background: var(--accent); color: #1a1a1a; text-align: center; font-weight: 700;
                    padding: 9px 16px; font-size: .9rem; }
        .announce .count b { font-variant-numeric: tabular-nums; }
        /* nav fixa */
        .topnav { position: sticky; top: 0; z-index: 50; background: color-mix(in srgb, #ffffff 88%, transparent);
                  -webkit-backdrop-filter: blur(8px); backdrop-filter: blur(8px);
                  border-bottom: 1px solid rgba(125,125,125,.16); }
        .topnav .wrap { display: flex; align-items: center; gap: 16px; padding-top: 12px; padding-bottom: 12px; }
        .nav-brand { font-weight: 800; color: #1a1a1a; }
        .nav-links { display: flex; gap: 18px; margin-left: auto; }
        .nav-links a { color: #1a1a1a; text-decoration: none; font-weight: 600; font-size: .92rem; opacity: .75; }
        .nav-links a:hover { opacity: 1; }
        .nav-cta { margin-left: 18px; padding: 9px 18px !important; font-size: .9rem !important; box-shadow: none !important; }
        /* CTA fixo no mobile */
        .sticky-cta { display: none; }
        /* FAQ accordion (nativo, sem JS) */
        .faq-item { border-bottom: 1px solid rgba(125,125,125,.18); padding: 6px 0; }
        .faq-item summary { cursor: pointer; font-weight: 700; padding: 12px 0; list-style: none;
                            display: flex; justify-content: space-between; align-items: center; gap: 12px; }
        .faq-item summary::-webkit-details-marker { display: none; }
        .faq-item summary::after { content: "+"; color: var(--accent); font-weight: 800; font-size: 1.3rem; }
        .faq-item[open] summary::after { content: "−"; }
        .faq-item p { margin: 0 0 12px; opacity: .85; }
        /* rodapé legal */
        footer .f-pay { font-weight: 600; margin-bottom: 10px; }
        footer .f-pay .ic { color: var(--accent); vertical-align: -.15em; margin-right: 4px; }
        footer .f-disclaimer { font-size: .76rem; opacity: .7; max-width: 660px; margin: 0 auto 12px; line-height: 1.5; }
        footer .f-legal { margin-bottom: 8px; }
        footer .f-links { display: flex; gap: 16px; justify-content: center; flex-wrap: wrap; }
        footer .f-links a { color: inherit; opacity: .85; text-decoration: none; }
        footer .f-links a:hover { opacity: 1; text-decoration: underline; }
        @media (max-width: 720px) {
          .nav-links { display: none; }
          .nav-cta { margin-left: auto; }
          body { padding-bottom: 78px; }
          .sticky-cta { position: fixed; bottom: 0; left: 0; right: 0; z-index: 60; display: flex;
                        align-items: center; justify-content: space-between; gap: 12px; padding: 12px 16px;
                        background: #fff; border-top: 1px solid rgba(125,125,125,.2);
                        box-shadow: 0 -6px 20px rgba(0,0,0,.12); }
          .sticky-cta .sc-price { font-weight: 800; color: #1a1a1a; }
          .sticky-cta .cta { padding: 12px 20px; font-size: 1rem; box-shadow: none; }
        }
        .proof-pill { display: inline-block; background: color-mix(in srgb, var(--accent) 22%, transparent);
                      color: var(--accent); font-weight: 800; font-size: .74rem; padding: 6px 14px;
                      border-radius: 999px; margin-bottom: 18px;
                      text-transform: uppercase; letter-spacing: .1em; }
        .hero-proof { display: flex; flex-wrap: wrap; gap: 10px 16px; align-items: center; margin-top: 22px; font-size: .9rem; }
        .hero-proof .avatars { display: inline-flex; }
        .hero-proof .avatars i { width: 30px; height: 30px; border-radius: 50%; margin-left: -10px;
            border: 2px solid #fff; background: linear-gradient(135deg, var(--accent), var(--bg)); }
        .hero-proof .avatars i:first-child { margin-left: 0; }
        .hero-proof .stars { color: var(--accent); letter-spacing: 2px; }
        .hero-proof .rating-text { font-weight: 600; }
        .hero-proof .badge { display: inline-flex; align-items: center; gap: 6px; padding: 5px 12px;
                             border-radius: 999px; background: rgba(125,125,125,.16); font-weight: 600; font-size: .82rem; }
        .ic { width: 1em; height: 1em; display: inline-block; vertical-align: -.125em; flex: none; }
        .hero-proof .badge .ic { color: var(--accent); }
        .showcase img { width: 100%; border-radius: 16px; display: block; box-shadow: 0 20px 50px rgba(0,0,0,.16); }
        .media-bar { padding: 28px 0; background: rgba(125,125,125,.06); border-block: 1px solid rgba(125,125,125,.14); }
        .media-bar .wrap { display: flex; flex-direction: column; align-items: center; gap: 12px; }
        .media-label { text-transform: uppercase; letter-spacing: .12em; font-size: .72rem; opacity: .65; }
        .media-logos { display: flex; flex-wrap: wrap; gap: 14px 28px; justify-content: center; }
        .media-logos span { font-weight: 800; letter-spacing: .04em; opacity: .55; font-size: 1rem; }
        .steps-list { list-style: none; display: grid; gap: 18px; counter-reset: s; }
        .steps-list li { display: flex; gap: 18px; align-items: flex-start; }
        .step-n { flex: none; width: 38px; height: 38px; border-radius: 50%; display: grid; place-items: center;
                  background: var(--accent); color: #1a1a1a; font-weight: 800; }
        .steps-list h3 { font-size: 1.15rem; margin-bottom: 4px; }
        .step-label { display: block; text-transform: uppercase; letter-spacing: .08em; font-size: .72rem;
                      color: var(--accent); font-weight: 700; }
        .stat-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 18px; }
        .stat { text-align: center; padding: 22px 14px; border-radius: 14px; background: rgba(125,125,125,.07);
                border: 1px solid rgba(125,125,125,.14); }
        .stat-v { display: block; font-size: 2.1rem; font-weight: 800; color: var(--accent); }
        .stat-l { font-size: .9rem; opacity: .8; }
        .tcards { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 18px; }
        .tcard { margin: 0; padding: 22px; border-radius: 14px; background: rgba(125,125,125,.07);
                 border: 1px solid rgba(125,125,125,.14); }
        .tcard blockquote { margin: 0 0 14px; font-size: 1.02rem; }
        .tcard blockquote::before { content: "\201C"; color: var(--accent); font-size: 1.6rem; margin-right: 2px; }
        .tresult { display: inline-block; margin-bottom: 10px; padding: 4px 10px; border-radius: 999px;
                   background: color-mix(in srgb, var(--accent) 20%, transparent); color: var(--accent);
                   font-weight: 700; font-size: .78rem; }
        .tcard figcaption strong { display: block; }
        .tcard figcaption span { font-size: .85rem; opacity: .7; }
        .author-card { display: flex; gap: 20px; padding: 26px; border-radius: 16px; background: rgba(125,125,125,.07);
                       border: 1px solid rgba(125,125,125,.14); }
        .author-title { display: block; color: var(--accent); font-weight: 700; }
        .author-cred { display: block; font-size: .85rem; opacity: .7; margin-bottom: 10px; }
        .author-hl { margin: 12px 0 0; padding-left: 18px; opacity: .85; }
        .bonus-stack { list-style: none; display: grid; gap: 10px; text-align: left; max-width: 540px;
                       margin: 0 auto 14px; }
        .bonus-stack li { display: flex; justify-content: flex-start; gap: 12px; align-items: center;
                          padding: 12px 16px; border-radius: 12px; background: rgba(125,125,125,.12);
                          border: 1px solid rgba(125,125,125,.18); }
        .bonus-stack .ic { color: var(--accent); width: 22px; height: 22px; }
        .bonus-info { flex: 1; }
        .bonus-name { display: block; font-weight: 700; }
        .bonus-desc { display: block; font-size: .85rem; opacity: .75; }
        .bonus-val { flex: none; margin-left: auto; font-weight: 800; color: var(--accent); }
        .bonus-total { opacity: .8; margin-bottom: 18px; }
        .bonuses { list-style: none; display: inline-grid; gap: 10px; text-align: left; margin: 0 auto 22px; }
        .bonuses li::before { content: "★ "; color: var(--accent); }
        .installments { opacity: .85; margin-bottom: 22px; font-size: 1.05rem; }
        .trust-row { display: flex; flex-wrap: wrap; gap: 10px 18px; justify-content: center; margin-top: 18px;
                     font-size: .85rem; opacity: .85; }
        .trust-row span { display: inline-flex; align-items: center; gap: 6px; }
        .trust-row .ic { color: var(--accent); }
        .guarantee .g-card { display: flex; gap: 22px; align-items: center; padding: 28px; border-radius: 16px;
                             background: rgba(125,125,125,.07); border: 2px dashed var(--accent); }
        .g-badge { flex: none; width: 88px; height: 88px; border-radius: 50%; display: flex; flex-direction: column;
                   align-items: center; justify-content: center; background: var(--accent); color: #1a1a1a;
                   font-weight: 800; line-height: 1; }
        .g-badge .ic { width: 22px; height: 22px; margin-bottom: 3px; }
        .g-badge b { font-size: 1.7rem; }
        .g-badge small { font-size: .7rem; font-weight: 700; }
        .guarantee h2 { margin-bottom: 8px; }
        /* tipografia premium (docs/16 §5): escala fixa (12-72) + tracking + quebra equilibrada */
        :root { --fs-xs: .75rem; --fs-sm: .875rem; --fs-base: 1rem; --fs-md: 1.125rem;
                --fs-lg: 1.5rem; --fs-xl: 2rem; --fs-2xl: 3rem; --fs-3xl: 4.5rem; }
        h1, h2 { letter-spacing: -0.02em; text-wrap: balance; }
        h3 { letter-spacing: -0.01em; }
        /* hero 2.0 (docs/15 Frente D): profundidade com glows da paleta + mockup 3D */
        .hero { position: relative; overflow: hidden; }
        .hero::before, .hero::after { content: ""; position: absolute; border-radius: 50%;
            filter: blur(90px); z-index: 0; pointer-events: none; }
        .hero::before { width: 440px; height: 440px; background: var(--accent); opacity: .45;
            top: -130px; right: -90px; }
        .hero::after { width: 380px; height: 380px; background: var(--bg); opacity: .22;
            bottom: -150px; left: -110px; }
        .hero > .wrap { position: relative; z-index: 1; }
        .hero-media { position: relative; display: inline-block; }
        .hero-art { transition: transform .3s ease; }
        .hero-art:hover { transform: translateY(-4px) rotate(-.5deg); }
        .hero-badge { position: absolute; bottom: 14px; left: -14px; display: flex; flex-direction: column;
            background: var(--accent); color: #1a1a1a; padding: 10px 16px; border-radius: 16px;
            box-shadow: 0 12px 30px color-mix(in srgb, var(--accent) 40%, transparent); transform: rotate(-3deg); }
        .hero-badge b { font-size: 1.5rem; font-weight: 800; line-height: 1; }
        .hero-badge i { font-style: normal; font-size: .72rem; text-transform: uppercase; letter-spacing: .04em; }
        .final-cta { text-align: center; }
        .final-cta h2 { margin-bottom: 14px; }
        .final-cta p { max-width: 620px; margin: 0 auto 26px; opacity: .85; }
        @media (max-width: 720px) {
          .author-card { flex-direction: column; }
          .g-card { flex-direction: column; text-align: center; }
        }
        """;

    // pixel sem src + script que repassa os utm_* da URL ao pixel (uma batida) e aos CTAs (E11-01)
    private static string Pixel(LpModel m) =>
        "<img id=\"tomo-px\" alt=\"\" width=\"1\" height=\"1\" style=\"position:absolute;left:-9999px\" />" +
        "<script>(function(){var b='" + JsString(m.PixelUrl) + "';" +
        "var p=new URLSearchParams(location.search);" +
        "var u=['utm_source','utm_campaign','utm_content']" +
        ".filter(function(k){return p.get(k);})" +
        ".map(function(k){return k+'='+encodeURIComponent(p.get(k));}).join('&');" +
        "var px=document.getElementById('tomo-px');if(px){px.src=b+(u?'&'+u:'');}" +
        "document.querySelectorAll('a.cta').forEach(function(a){" +
        "if(u){a.href=a.href+(a.href.indexOf('?')>-1?'&':'?')+u;}});" +
        "})();</script>";

    private static string Money(decimal value, string currency)
    {
        var symbol = currency.Equals("BRL", StringComparison.OrdinalIgnoreCase) ? "R$" : currency + " ";
        return $"{symbol} {value.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"))}";
    }

    private static string Font(string family) =>
        family.Contains(' ') ? $"'{family}', sans-serif" : $"{family}, sans-serif";

    // escapa apenas os caracteres perigosos para HTML; acentos pt-BR passam literais (HTML mais limpo)
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));

    private static string Esc(string value) => Encoder.Encode(value);

    // escapa uma string para um literal JS entre aspas simples (slug é controlado, mas previne quebra)
    private static string JsString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("<", "\\x3c", StringComparison.Ordinal);
}
