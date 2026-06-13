using System.Text;

namespace Ebook.Application.Content.Pdf;

public enum PdfTheme
{
    Classic,
    Modern,
    Editorial
}

public sealed record PdfCta(string Headline, string? Url);

/// <summary>Modelo pronto para renderização em PDF: capa + corpo (blocos) + página de CTA.</summary>
public sealed record PdfBook(
    string Title,
    string? Subtitle,
    string? Tagline,
    PdfTheme Theme,
    IReadOnlyList<MarkdownBlock> Body,
    PdfCta Cta);

/// <summary>Seleção determinística de tema por nicho (mesmo nicho → mesmo tema, estável entre processos).</summary>
public static class PdfThemeSelector
{
    public static PdfTheme ForNiche(string slug)
    {
        var hash = 2166136261u; // FNV-1a 32-bit, estável (não usar string.GetHashCode)
        foreach (var b in Encoding.UTF8.GetBytes(slug ?? string.Empty))
        {
            hash = (hash ^ b) * 16777619u;
        }

        return (PdfTheme)(hash % 3);
    }
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
        PdfTheme theme)
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

        return new PdfBook(title, subtitle, tagline, theme, body, cta);
    }
}
