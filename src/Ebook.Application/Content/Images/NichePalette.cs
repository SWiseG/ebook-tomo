using System.Globalization;
using System.Text;

namespace Ebook.Application.Content.Images;

/// <summary>
/// Identidade visual de um nicho (E09-03): cores em hex e famílias tipográficas.
/// Serializável como config JSON por nicho/produto; quando ausente, usa-se o catálogo padrão.
/// <para>
/// <see cref="DisplayFont"/> (docs/14 WP-3) é a fonte de IMPACTO da capa/cards sociais — display,
/// condensada ou black. O <see cref="HeadingFont"/> segue refinado para os títulos do PDF (leitura).
/// Opcional: ausente, a capa cai no <see cref="HeadingFont"/> (compatível com paletas antigas).
/// </para>
/// </summary>
public sealed record NichePalette(
    string Background,
    string Accent,
    string OnDark,
    string HeadingFont,
    string BodyFont,
    string? DisplayFont = null)
{
    /// <summary>Fonte de exibição da capa/cards: <see cref="DisplayFont"/> ou, na falta, o título.</summary>
    public string Display => string.IsNullOrWhiteSpace(DisplayFont) ? HeadingFont : DisplayFont!;
}

/// <summary>Categoria editorial inferida do nicho — guia cor, tipografia e tom (docs/11).</summary>
public enum NicheCategory
{
    Finance,
    Health,
    SelfHelp,
    Marketing,
    Tech,
    Fiction,
    Education,
    General
}

/// <summary>
/// Catálogo de estilo POR NICHO (não mais por hash). Mapeia o nicho para uma paleta com
/// cores emocionais (psicologia das cores, regra 60-30-10) e pareamento de fontes profissionais
/// — serifa para corpo, sans/serifa-display para título. Ver docs/11-padrao-editorial.md.
/// As fontes referenciadas são embarcadas via FontRegistry; sem elas, há fallback gracioso.
/// </summary>
public static class NicheStyleCatalog
{
    public static NicheCategory Classify(string nicheNameOrSlug)
    {
        var s = Normalize(nicheNameOrSlug);

        if (HasAny(s, "financ", "dinheiro", "invest", "renda", "money", "econom", "divida", "orcament", "riqueza", "cripto", "bolsa"))
        {
            return NicheCategory.Finance;
        }

        if (HasAny(s, "saude", "emagre", "fitness", "dieta", "bem-estar", "bem estar", "wellness", "nutri", "treino", "academia", "gordura", "peso"))
        {
            return NicheCategory.Health;
        }

        if (HasAny(s, "marketing", "vendas", "trafego", "copy", "negocio", "empreend", "afiliad", "lancamento", "digital"))
        {
            return NicheCategory.Marketing;
        }

        if (HasAny(s, "desenvolv", "autoajuda", "auto-ajuda", "coaching", "produtiv", "habito", "mindset", "proposito", "autoconhec", "motiva", "disciplina",
            "relacion", "casal", "casamento", "namoro", "amor", "afetiv"))
        {
            return NicheCategory.SelfHelp;
        }

        if (HasAny(s, "tech", "tecnolog", "inteligencia artificial", "saas", "software", "program", "dados", "automacao", "app"))
        {
            return NicheCategory.Tech;
        }

        if (HasAny(s, "ficcao", "romance", "conto", "novela", "fantasia", "suspense", "poesia"))
        {
            return NicheCategory.Fiction;
        }

        if (HasAny(s, "educa", "ensino", "escola", "concurso", "idioma", "ingles", "espanhol", "estudo", "vestibular", "aprend"))
        {
            return NicheCategory.Education;
        }

        return NicheCategory.General;
    }

    /// <summary>Fontes embarcadas (FontRegistry). A IA de paleta só pode escolher destas (docs/14 WP-2/3).</summary>
    public static readonly IReadOnlySet<string> AllowedFonts = new HashSet<string>(StringComparer.Ordinal)
    {
        "Inter", "Manrope", "Lora", "Merriweather", "Fraunces", "Playfair Display",
        "Anton", "Archivo Black", "Bebas Neue", "Fjalla One", "Barlow Condensed",
        "Space Grotesk", "Cormorant", "DM Sans",
    };

    public static NichePalette Palette(string nicheNameOrSlug) => For(Classify(nicheNameOrSlug));

    // Cor dominante (Background) + destaque (Accent, ~10%) + texto claro (OnDark) + título PDF +
    // corpo + fonte de IMPACTO da capa (DisplayFont, docs/14 WP-3). Todas embarcadas (FontRegistry).
    public static NichePalette For(NicheCategory category) => category switch
    {
        // docs/16 §6: navy+dourado (confiança) · sálvia+terracota+creme (wellness) · slate+neon (tech)
        NicheCategory.Finance => new("#0E2A47", "#E0B978", "#F5F8FC", "Manrope", "Merriweather", "Archivo Black"),
        NicheCategory.Health => new("#2F4A3C", "#C2654A", "#F5F0E8", "Fraunces", "DM Sans", "Cormorant"),
        NicheCategory.SelfHelp => new("#7C2D12", "#FDBA74", "#FFF7ED", "Fraunces", "DM Sans", "Anton"),
        NicheCategory.Marketing => new("#111827", "#F2552C", "#F9FAFB", "Manrope", "Inter", "Anton"),
        NicheCategory.Tech => new("#0F172A", "#5EEAD4", "#E2E8F0", "Space Grotesk", "Inter", "Space Grotesk"),
        NicheCategory.Fiction => new("#3B0764", "#D8B4FE", "#FAF5FF", "Playfair Display", "Lora", "Playfair Display"),
        NicheCategory.Education => new("#0C4A6E", "#7DD3FC", "#F0F9FF", "Merriweather", "Inter", "Barlow Condensed"),
        NicheCategory.General or _ => new("#1F2937", "#CBA15A", "#F9FAFB", "Manrope", "Merriweather", "Bebas Neue"),
    };

    /// <summary>
    /// Disclaimer legal honesto por categoria (Fase 5 / docs/12). Texto fixo de proteção — não é
    /// conteúdo gerado por IA nem promessa; protege a operação e respeita CDC/órgãos reguladores.
    /// </summary>
    public static string DisclaimerFor(NicheCategory category) => category switch
    {
        NicheCategory.Finance =>
            "Este conteúdo é educacional e não constitui recomendação de investimento. Resultados variam conforme a aplicação individual.",
        NicheCategory.Health =>
            "Este conteúdo é educacional e não substitui orientação médica, nutricional ou de profissional de saúde. Consulte um especialista.",
        NicheCategory.SelfHelp =>
            "Este conteúdo é educacional e não substitui acompanhamento psicológico ou terapêutico profissional. Os resultados variam.",
        NicheCategory.Marketing =>
            "Este conteúdo é educacional. Resultados dependem de esforço, mercado e aplicação; não há garantia de ganhos.",
        NicheCategory.Education =>
            "Este conteúdo é educacional e complementar; os resultados variam conforme a dedicação individual.",
        NicheCategory.Fiction =>
            "Obra de ficção. Nomes, personagens e acontecimentos são fruto da imaginação do autor.",
        _ =>
            "Este conteúdo é educacional. Os resultados podem variar de pessoa para pessoa.",
    };

    private static bool HasAny(string normalized, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (normalized.Contains(n, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // minúsculas + remoção de acentos (NFD) para casar palavras-chave de forma robusta
    private static string Normalize(string value)
    {
        var lowered = (value ?? string.Empty).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}

/// <summary>Seleção de paleta por nicho. Delegada ao <see cref="NicheStyleCatalog"/> (semântica).</summary>
public static class PaletteCatalog
{
    public static NichePalette ForNiche(string slug) => NicheStyleCatalog.Palette(slug);
}
