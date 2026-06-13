using Ebook.Application.Content.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ebook.Infrastructure.Content;

/// <summary>
/// Renderiza o e-book em PDF com QuestPDF (licença Community). Três temas profissionais
/// (capa colorida, sumário, tipografia, cabeçalho/rodapé com paginação e página de CTA).
/// </summary>
public sealed class QuestPdfRenderer : IPdfRenderer
{
    static QuestPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(PdfBook book, byte[]? coverImage = null)
    {
        var theme = Style.For(book.Theme);

        return Document.Create(doc =>
        {
            doc.Page(cover => ComposeCover(cover, book, theme, coverImage));
            doc.Page(content =>
            {
                content.Size(PageSizes.A5);
                content.Margin(42);
                content.DefaultTextStyle(x => x.FontFamily(theme.BodyFont).FontSize(11).FontColor(theme.Text).LineHeight(1.4f));
                content.Header().Element(h => ComposeHeader(h, book, theme));
                content.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(9).FontColor("#9CA3AF"));
                    t.CurrentPageNumber();
                });
                content.Content().Column(col => ComposeBody(col, book, theme));
            });
        }).GeneratePdf();
    }

    private static void ComposeCover(PageDescriptor cover, PdfBook book, Style theme, byte[]? coverImage)
    {
        cover.Size(PageSizes.A5);
        cover.Margin(0);

        if (coverImage is { Length: > 0 })
        {
            // capa gerada pelo Image Generator (E09): imagem full-bleed
            cover.Content().Image(coverImage).FitArea();
            return;
        }

        cover.Content().Background(theme.Primary).Padding(46).Column(col =>
        {
            col.Item().PaddingTop(110).Text(book.Title)
                .FontFamily(theme.HeadingFont).FontSize(30).Bold().FontColor("#FFFFFF");

            if (!string.IsNullOrWhiteSpace(book.Subtitle))
            {
                col.Item().PaddingTop(14).Text(book.Subtitle)
                    .FontFamily(theme.HeadingFont).FontSize(16).FontColor(theme.Accent);
            }

            if (!string.IsNullOrWhiteSpace(book.Tagline))
            {
                col.Item().PaddingTop(38).Text(book.Tagline)
                    .FontFamily(theme.BodyFont).FontSize(12).Italic().FontColor("#E5E7EB");
            }
        });
    }

    private static void ComposeHeader(IContainer header, PdfBook book, Style theme) =>
        header.PaddingBottom(6).BorderBottom(1).BorderColor(theme.Accent)
            .Text(book.Title).FontFamily(theme.BodyFont).FontSize(8).FontColor("#9CA3AF");

    private static void ComposeBody(ColumnDescriptor col, PdfBook book, Style theme)
    {
        var chapters = book.Body
            .Where(b => b.Kind == MarkdownBlockKind.Heading && b.Level == 2
                && b.Text.StartsWith("Capítulo", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (chapters.Count > 0)
        {
            col.Item().Text("Sumário").FontFamily(theme.HeadingFont).FontSize(20).Bold().FontColor(theme.Primary);
            col.Item().PaddingBottom(8);
            foreach (var chapter in chapters)
            {
                col.Item().PaddingVertical(2).Text(chapter.Text).FontSize(11).FontColor(theme.Text);
            }
        }

        foreach (var block in book.Body)
        {
            ComposeBlock(col, block, theme);
        }

        col.Item().PageBreak();
        ComposeCta(col, book, theme);
    }

    private static void ComposeBlock(ColumnDescriptor col, MarkdownBlock block, Style theme)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading when block.Level == 2:
                col.Item().PageBreak();
                col.Item().PaddingBottom(10).Text(block.Text)
                    .FontFamily(theme.HeadingFont).FontSize(22).Bold().FontColor(theme.Primary);
                break;

            case MarkdownBlockKind.Heading: // nível 3 (e demais subtítulos)
                col.Item().PaddingTop(8).PaddingBottom(4).Text(block.Text)
                    .FontFamily(theme.HeadingFont).FontSize(14).SemiBold().FontColor(theme.Accent);
                break;

            case MarkdownBlockKind.Bullets:
                foreach (var item in block.Items)
                {
                    col.Item().PaddingLeft(6).Row(row =>
                    {
                        row.ConstantItem(14).Text("•").FontColor(theme.Accent);
                        row.RelativeItem().Text(item);
                    });
                }

                break;

            case MarkdownBlockKind.Paragraph:
            default:
                col.Item().PaddingBottom(8).Text(block.Text).Justify();
                break;
        }
    }

    private static void ComposeCta(ColumnDescriptor col, PdfBook book, Style theme)
    {
        col.Item().Background(theme.Primary).Padding(28).Column(cta =>
        {
            cta.Item().Text(book.Cta.Headline)
                .FontFamily(theme.HeadingFont).FontSize(18).Bold().FontColor("#FFFFFF");

            var link = string.IsNullOrWhiteSpace(book.Cta.Url) ? "Disponível em breve." : book.Cta.Url;
            cta.Item().PaddingTop(12).Text(link).FontFamily(theme.BodyFont).FontSize(12).FontColor(theme.Accent);
        });
    }

    // Famílias resolvíveis em Windows (nativas) e Linux (aliases do fonts-liberation):
    // "Arial" → Liberation Sans, "Times New Roman" → Liberation Serif.
    private sealed record Style(string Primary, string Accent, string Text, string HeadingFont, string BodyFont)
    {
        public static Style For(PdfTheme theme) => theme switch
        {
            PdfTheme.Modern => new Style("#0F766E", "#5EEAD4", "#1F2937", "Arial", "Arial"),
            PdfTheme.Editorial => new Style("#7C2D12", "#FDBA74", "#292524", "Times New Roman", "Arial"),
            PdfTheme.Classic or _ => new Style("#1F2937", "#CBA15A", "#111827", "Times New Roman", "Times New Roman")
        };
    }
}
