using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ebook.Infrastructure.Content;

/// <summary>
/// Renderiza o e-book em PDF com QuestPDF (licença Community). Cores e tipografia vêm da paleta
/// do nicho (docs/11), com hierarquia profissional (capa, sumário, cabeçalho/rodapé, página de CTA).
/// Fontes embarcadas via <see cref="FontRegistry"/>; ausentes, há fallback (Lato padrão do QuestPDF).
/// </summary>
public sealed class QuestPdfRenderer : IPdfRenderer
{
    static QuestPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // não lança exceção quando um glifo/fonte falta: cai no fallback (fonte pode não estar embarcada)
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public byte[] Render(PdfBook book, byte[]? coverImage = null)
    {
        // paleta do nicho tem prioridade (coerência com a capa); sem ela, cai no tema (compat)
        var theme = book.Palette is { } palette ? Style.From(palette) : Style.For(book.Theme);

        return Document.Create(doc =>
        {
            doc.Page(cover => ComposeCover(cover, book, theme, coverImage));
            doc.Page(content =>
            {
                content.Size(PageSizes.A5);
                content.Margin(42);
                content.DefaultTextStyle(x => x.FontFamily(theme.BodyFont).FontSize(12).FontColor(theme.Text).LineHeight(1.5f));
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
                .FontFamily(theme.HeadingFont).FontSize(40).Bold().FontColor("#FFFFFF");

            if (!string.IsNullOrWhiteSpace(book.Subtitle))
            {
                col.Item().PaddingTop(16).Text(book.Subtitle)
                    .FontFamily(theme.HeadingFont).FontSize(18).FontColor(theme.Accent);
            }

            if (!string.IsNullOrWhiteSpace(book.Tagline))
            {
                col.Item().PaddingTop(38).Text(book.Tagline)
                    .FontFamily(theme.BodyFont).FontSize(13).Italic().FontColor("#E5E7EB");
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
            col.Item().Text("Sumário").FontFamily(theme.HeadingFont).FontSize(24).Bold().FontColor(theme.Primary);
            col.Item().PaddingBottom(8);
            foreach (var chapter in chapters)
            {
                col.Item().PaddingVertical(2).Text(chapter.Text).FontSize(12).FontColor(theme.Text);
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
                col.Item().PaddingBottom(12).Text(block.Text)
                    .FontFamily(theme.HeadingFont).FontSize(26).Bold().FontColor(theme.Primary);
                break;

            case MarkdownBlockKind.Heading: // nível 3 (e demais subtítulos)
                col.Item().PaddingTop(10).PaddingBottom(4).Text(block.Text)
                    .FontFamily(theme.HeadingFont).FontSize(18).SemiBold().FontColor(theme.Primary);
                break;

            case MarkdownBlockKind.Bullets:
                foreach (var item in block.Items)
                {
                    var (isTask, done, text) = ParseTask(item);
                    col.Item().PaddingLeft(6).PaddingVertical(1).Row(row =>
                    {
                        if (isTask)
                        {
                            // checkbox desenhado (sem glifo): quadrado vazado ou preenchido com o accent
                            row.ConstantItem(18).AlignMiddle().Element(box => box
                                .Width(11).Height(11).Border(1.2f).BorderColor(theme.Accent)
                                .Background(done ? theme.Accent : "#FFFFFF"));
                        }
                        else
                        {
                            row.ConstantItem(14).Text("•").FontColor(theme.Accent);
                        }

                        row.RelativeItem().Text(text);
                    });
                }

                break;

            case MarkdownBlockKind.PullQuote:
                // citação de impacto: barra de accent + texto grande em itálico (cor do título)
                col.Item().PaddingVertical(10).Row(row =>
                {
                    row.ConstantItem(4).Background(theme.Accent);
                    row.ConstantItem(14);
                    row.RelativeItem().Text(block.Text)
                        .FontFamily(theme.HeadingFont).FontSize(15).Italic().FontColor(theme.Primary).LineHeight(1.4f);
                });
                break;

            case MarkdownBlockKind.Callout:
                // caixa de destaque (Insight rápido / Estudo de caso): ícone SVG + borda accent + fundo claro
                col.Item().PaddingVertical(10)
                    .BorderLeft(3).BorderColor(theme.Accent)
                    .Background("#F5F5F3")
                    .PaddingVertical(12).PaddingHorizontal(16)
                    .Column(box =>
                    {
                        box.Item().Row(head =>
                        {
                            var icon = IconRegistry.Colored(IconFor(block.Label), theme.Primary);
                            if (icon is not null)
                            {
                                head.ConstantItem(16).AlignMiddle().Svg(icon);
                                head.ConstantItem(8);
                            }

                            head.RelativeItem().AlignMiddle().Text(block.Label.ToUpperInvariant())
                                .FontFamily(theme.HeadingFont).FontSize(10).Bold().FontColor(theme.Primary);
                        });
                        box.Item().PaddingTop(6).Text(block.Text).FontColor(theme.Text).LineHeight(1.5f);
                    });
                break;

            case MarkdownBlockKind.Image:
                if (block.ImageBytes is { Length: > 256 } img)
                {
                    col.Item().PaddingVertical(14).MaxHeight(190).Image(img).FitWidth();
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
                .FontFamily(theme.HeadingFont).FontSize(20).Bold().FontColor("#FFFFFF");

            var link = string.IsNullOrWhiteSpace(book.Cta.Url) ? "Disponível em breve." : book.Cta.Url;
            cta.Item().PaddingTop(12).Text(link).FontFamily(theme.BodyFont).FontSize(12).FontColor(theme.Accent);
        });
    }

    // ícone Lucide por tipo de caixa (docs/12): Estudo de caso → tendência; Insight → lâmpada
    private static string IconFor(string label) =>
        label.StartsWith("Estudo", StringComparison.OrdinalIgnoreCase) ? "trending-up" : "lightbulb";

    // "[ ] tarefa" → (true,false,"tarefa") · "[x] feita" → (true,true,"feita") · resto → bullet normal
    private static (bool IsTask, bool Done, string Text) ParseTask(string item)
    {
        if (item.StartsWith("[ ] ", StringComparison.Ordinal))
        {
            return (true, false, item[4..]);
        }

        if (item.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
        {
            return (true, true, item[4..]);
        }

        return (false, false, item);
    }

    private sealed record Style(string Primary, string Accent, string Text, string HeadingFont, string BodyFont)
    {
        // Derivado da paleta do nicho (caminho principal): cor de cabeçalho = fundo do nicho,
        // texto do corpo num quase-preto legível sobre branco, fontes profissionais embarcadas.
        public static Style From(NichePalette p) =>
            new(p.Background, p.Accent, "#1A1A1A", p.HeadingFont, p.BodyFont);

        // Fallback por tema (usado quando não há paleta, ex.: testes). Fontes embarcadas via FontRegistry.
        public static Style For(PdfTheme theme) => theme switch
        {
            PdfTheme.Modern => new Style("#1E1B4B", "#8B93F8", "#1A1A1A", "Manrope", "Inter"),
            PdfTheme.Editorial => new Style("#7C2D12", "#FDBA74", "#1A1A1A", "Fraunces", "Lora"),
            PdfTheme.Classic or _ => new Style("#0E2A47", "#E0B978", "#1A1A1A", "Manrope", "Merriweather")
        };
    }
}
