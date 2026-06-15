using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Ebook.Application.Content.Images;

namespace Ebook.Application.Content.Lp;

public enum LpTemplate
{
    Aurora,
    Editorial
}

/// <summary>Seleção determinística de template por nicho (mesmo nicho → mesmo template, estável entre processos).</summary>
public static class LpTemplateSelector
{
    public static LpTemplate ForNiche(string slug)
    {
        var hash = 2166136261u; // FNV-1a 32-bit, estável (não usar string.GetHashCode)
        foreach (var b in Encoding.UTF8.GetBytes(slug ?? string.Empty))
        {
            hash = (hash ^ b) * 16777619u;
        }

        return (LpTemplate)(hash % 2);
    }
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
    NichePalette Palette);

/// <summary>
/// Constrói a landing page como HTML auto-contido (CSS inline, capa em data URI),
/// pronta para servir estaticamente. Função pura — sem dependência de infraestrutura.
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
        NichePalette palette)
    {
        var headline = string.IsNullOrWhiteSpace(copy?.Headline) ? productTitle : copy!.Headline!;
        var bullets = (copy?.Bullets ?? []).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        var faq = (copy?.Faq ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Q) && !string.IsNullOrWhiteSpace(f.A))
            .ToList();
        var bonuses = (copy?.Bonuses ?? []).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        var coverDataUri = coverImage is { Length: > 0 }
            ? $"data:image/png;base64,{Convert.ToBase64String(coverImage)}"
            : null;

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
            palette);
    }

    public static string Render(LpModel model, LpTemplate template) => template switch
    {
        LpTemplate.Editorial => RenderEditorial(model),
        _ => RenderAurora(model)
    };

    // ---- Template Aurora: herói escuro com gradiente, CTA em destaque, sans moderno ----
    private static string RenderAurora(LpModel m)
    {
        var bg = m.Palette.Background;
        var accent = m.Palette.Accent;
        var onDark = m.Palette.OnDark;

        var css = $$"""
            :root { --bg: {{bg}}; --accent: {{accent}}; --on-dark: {{onDark}}; }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body { font-family: {{Font(m.Palette.BodyFont)}}; color: #1a1a1a; background: #fff; line-height: 1.6; }
            .wrap { max-width: 960px; margin: 0 auto; padding: 0 24px; }
            h1, h2, h3 { font-family: {{Font(m.Palette.HeadingFont)}}; line-height: 1.15; }
            .hero { background: linear-gradient(160deg, var(--bg), #000); color: var(--on-dark); padding: 72px 0; }
            .hero .grid { display: grid; grid-template-columns: 1.3fr 1fr; gap: 40px; align-items: center; }
            .hero h1 { font-size: 2.6rem; margin-bottom: 16px; }
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
            .price { font-size: 3rem; font-weight: 800; color: var(--accent); margin: 8px 0 24px; }
            .anchor { text-decoration: line-through; opacity: .6; font-size: 1.4rem; margin-right: 12px; }
            .bonuses { list-style: none; display: inline-grid; gap: 10px; text-align: left; margin: 0 auto 28px; }
            .bonuses li::before { content: "★ "; color: var(--accent); }
            .faq dt { font-weight: 700; margin-top: 20px; font-size: 1.1rem; }
            .faq dd { margin: 6px 0 0; opacity: .85; }
            footer { background: #111; color: #888; text-align: center; padding: 32px 0; font-size: .85rem; }
            @media (max-width: 720px) { .hero .grid { grid-template-columns: 1fr; } .hero h1 { font-size: 2rem; } }
            """;

        var sb = new StringBuilder();
        sb.Append(HtmlHead(m, css));
        sb.Append("<header class=\"hero\"><div class=\"wrap\"><div class=\"grid\"><div>");
        sb.Append($"<h1>{Esc(m.Headline)}</h1>");
        if (!string.IsNullOrWhiteSpace(m.Subheadline))
        {
            sb.Append($"<p class=\"sub\">{Esc(m.Subheadline!)}</p>");
        }
        sb.Append(CtaButton(m, "Quero agora"));
        sb.Append("</div><div>");
        if (m.CoverDataUri is not null)
        {
            sb.Append($"<img src=\"{m.CoverDataUri}\" alt=\"{Esc(m.Title)}\" />");
        }
        sb.Append("</div></div></div></header>");

        if (!string.IsNullOrWhiteSpace(m.PainSection))
        {
            sb.Append($"<section class=\"pain\"><div class=\"wrap\"><h2>O problema</h2><p>{Esc(m.PainSection!)}</p></div></section>");
        }

        if (!string.IsNullOrWhiteSpace(m.SolutionSection) || m.Bullets.Count > 0)
        {
            sb.Append("<section><div class=\"wrap\"><h2>A solução</h2>");
            if (!string.IsNullOrWhiteSpace(m.SolutionSection))
            {
                sb.Append($"<p style=\"margin-bottom:24px\">{Esc(m.SolutionSection!)}</p>");
            }
            sb.Append(BulletList(m.Bullets));
            sb.Append("</div></section>");
        }

        sb.Append("<section class=\"offer\"><div class=\"wrap\">");
        sb.Append($"<h2 style=\"color:var(--on-dark)\">{Esc(m.Title)}</h2>");
        sb.Append(PriceBlock(m));
        sb.Append(BonusList(m.Bonuses));
        sb.Append(CtaButton(m, "Garantir meu acesso"));
        sb.Append("</div></section>");

        sb.Append(FaqBlock(m.Faq));
        sb.Append(Footer(m));
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
            :root { --ink: {{bg}}; --accent: {{accent}}; }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body { font-family: {{Font(m.Palette.BodyFont)}}; color: #2b2b2b; background: #fdfcf9; line-height: 1.7; }
            .wrap { max-width: 760px; margin: 0 auto; padding: 0 24px; }
            h1, h2, h3 { font-family: {{Font(m.Palette.HeadingFont)}}; color: var(--ink); line-height: 1.2; }
            .hero { text-align: center; padding: 80px 0 56px; border-bottom: 3px solid var(--accent); }
            .hero h1 { font-size: 2.8rem; margin-bottom: 18px; }
            .hero .sub { font-size: 1.25rem; color: #555; max-width: 600px; margin: 0 auto 32px; }
            .hero img { width: 320px; max-width: 80%; margin: 36px auto 0; display: block;
                        box-shadow: 0 18px 50px rgba(0,0,0,.18); border-radius: 6px; }
            .cta { display: inline-block; background: var(--ink); color: #fff; font-weight: 700;
                   padding: 15px 36px; border-radius: 4px; text-decoration: none; font-size: 1.05rem;
                   letter-spacing: .3px; }
            .cta:hover { background: var(--accent); color: var(--ink); }
            section { padding: 52px 0; border-bottom: 1px solid #ece7dd; }
            section h2 { font-size: 1.8rem; margin-bottom: 20px; }
            section h2::before { content: "—"; color: var(--accent); margin-right: 10px; }
            .bullets { list-style: none; display: grid; gap: 12px; }
            .bullets li { padding-left: 28px; position: relative; }
            .bullets li::before { content: "→"; position: absolute; left: 0; color: var(--accent); font-weight: 700; }
            .offer { text-align: center; }
            .price { font-size: 2.8rem; font-weight: 800; color: var(--ink); margin: 10px 0 22px; }
            .anchor { text-decoration: line-through; color: #999; font-size: 1.3rem; margin-right: 10px; }
            .bonuses { list-style: none; display: inline-grid; gap: 8px; text-align: left; margin: 0 auto 26px; }
            .bonuses li::before { content: "+ "; color: var(--accent); font-weight: 700; }
            .faq dt { font-weight: 700; margin-top: 18px; }
            .faq dd { margin: 4px 0 0; color: #555; }
            footer { text-align: center; padding: 30px 0; font-size: .85rem; color: #999; }
            @media (max-width: 720px) { .hero h1 { font-size: 2.1rem; } }
            """;

        var sb = new StringBuilder();
        sb.Append(HtmlHead(m, css));
        sb.Append("<header class=\"hero\"><div class=\"wrap\">");
        sb.Append($"<h1>{Esc(m.Headline)}</h1>");
        if (!string.IsNullOrWhiteSpace(m.Subheadline))
        {
            sb.Append($"<p class=\"sub\">{Esc(m.Subheadline!)}</p>");
        }
        sb.Append(CtaButton(m, "Começar agora"));
        if (m.CoverDataUri is not null)
        {
            sb.Append($"<img src=\"{m.CoverDataUri}\" alt=\"{Esc(m.Title)}\" />");
        }
        sb.Append("</div></header>");

        if (!string.IsNullOrWhiteSpace(m.PainSection))
        {
            sb.Append($"<section><div class=\"wrap\"><h2>Por que isso importa</h2><p>{Esc(m.PainSection!)}</p></div></section>");
        }

        if (!string.IsNullOrWhiteSpace(m.SolutionSection) || m.Bullets.Count > 0)
        {
            sb.Append("<section><div class=\"wrap\"><h2>O que você recebe</h2>");
            if (!string.IsNullOrWhiteSpace(m.SolutionSection))
            {
                sb.Append($"<p style=\"margin-bottom:22px\">{Esc(m.SolutionSection!)}</p>");
            }
            sb.Append(BulletList(m.Bullets));
            sb.Append("</div></section>");
        }

        sb.Append("<section class=\"offer\"><div class=\"wrap\">");
        sb.Append($"<h2 style=\"justify-content:center\">{Esc(m.Title)}</h2>");
        sb.Append(PriceBlock(m));
        sb.Append(BonusList(m.Bonuses));
        sb.Append(CtaButton(m, "Quero garantir"));
        sb.Append("</div></section>");

        sb.Append(FaqBlock(m.Faq));
        sb.Append(Footer(m));
        sb.Append(Pixel(m));
        sb.Append("</body></html>");
        return sb.ToString();
    }

    // ---- fragmentos compartilhados ----

    private static string HtmlHead(LpModel m, string css) =>
        $"""
        <!doctype html><html lang="pt-BR"><head><meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>{Esc(m.Headline)}</title>
        <meta name="description" content="{Esc(m.Subheadline ?? m.Headline)}" />
        <style>{css}</style></head><body>
        """;

    private static string CtaButton(LpModel m, string label) =>
        $"<a class=\"cta\" href=\"{Esc(m.CheckoutUrl)}\">{Esc(label)}</a>";

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

    private static string PriceBlock(LpModel m)
    {
        if (m.PriceCurrent <= 0m)
        {
            return string.Empty;
        }

        var anchor = m.PriceAnchor > m.PriceCurrent
            ? $"<span class=\"anchor\">{Money(m.PriceAnchor, m.Currency)}</span>"
            : string.Empty;
        return $"<div class=\"price\">{anchor}{Money(m.PriceCurrent, m.Currency)}</div>";
    }

    private static string FaqBlock(IReadOnlyList<LpFaqDto> faq)
    {
        if (faq.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<section class=\"faq\"><div class=\"wrap\"><h2>Perguntas frequentes</h2><dl>");
        foreach (var item in faq)
        {
            sb.Append($"<dt>{Esc(item.Q!)}</dt><dd>{Esc(item.A!)}</dd>");
        }
        return sb.Append("</dl></div></section>").ToString();
    }

    private static string Footer(LpModel m) =>
        $"<footer><div class=\"wrap\">{Esc(m.Title)} · Todos os direitos reservados</div></footer>";

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
