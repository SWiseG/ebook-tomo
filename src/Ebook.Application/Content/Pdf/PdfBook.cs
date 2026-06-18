using Ebook.Application.Content.Images;

namespace Ebook.Application.Content.Pdf;

public enum PdfTheme
{
    Classic,
    Modern,
    Editorial
}

public sealed record PdfCta(string Headline, string? Url);

/// <summary>
/// Modelo pronto para renderização em PDF: capa + corpo (blocos) + página de CTA.
/// <see cref="Palette"/> (cores+fontes do nicho) tem prioridade no renderizador; quando nula,
/// cai no estilo do <see cref="Theme"/> (compatibilidade).
/// </summary>
public sealed record PdfBook(
    string Title,
    string? Subtitle,
    string? Tagline,
    PdfTheme Theme,
    IReadOnlyList<MarkdownBlock> Body,
    PdfCta Cta,
    NichePalette? Palette = null);

/// <summary>Seleção de tema (flavor de layout) por nicho — semântica, alinhada ao NicheStyleCatalog.</summary>
public static class PdfThemeSelector
{
    public static PdfTheme ForNiche(string slug) => NicheStyleCatalog.Classify(slug) switch
    {
        NicheCategory.Tech or NicheCategory.Marketing => PdfTheme.Modern,
        NicheCategory.Health or NicheCategory.SelfHelp or NicheCategory.Fiction => PdfTheme.Editorial,
        _ => PdfTheme.Classic,
    };
}

/// <summary>
/// Monta um <see cref="PdfBook"/> a partir do manuscrito Markdown: separa a capa
/// (primeiro H1 e o subtítulo H2 inicial) do corpo, que segue para o renderizador.
/// </summary>
public static class PdfBookComposer
{
    public static PdfBook Build(
        string manuscript,
        string fallbackTitle,
        string? tagline,
        PdfCta cta,
        PdfTheme theme,
        NichePalette? palette = null)
    {
        var blocks = MarkdownParser.Parse(manuscript);
        var body = new List<MarkdownBlock>(blocks);

        string title = fallbackTitle;
        string? subtitle = null;

        if (body.Count > 0 && body[0] is { Kind: MarkdownBlockKind.Heading, Level: 1 } h1)
        {
            title = h1.Text;
            body.RemoveAt(0);
        }

        if (body.Count > 0
            && body[0] is { Kind: MarkdownBlockKind.Heading, Level: 2 } h2
            && !h2.Text.StartsWith("Capítulo", StringComparison.OrdinalIgnoreCase))
        {
            subtitle = h2.Text;
            body.RemoveAt(0);
        }

        return new PdfBook(title, subtitle, tagline, theme, body, cta, palette);
    }
}
