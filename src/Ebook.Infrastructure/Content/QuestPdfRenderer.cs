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

    // Cabeçalho corrente: título curto (não o título-promessa inteiro, que virava ruído) + régua accent.
    private static void ComposeHeader(IContainer header, PdfBook book, Style theme) =>
        header.PaddingBottom(6).BorderBottom(1).BorderColor(theme.Accent)
            .Text(Shorten(book.Title, 50)).FontFamily(theme.BodyFont).FontSize(8).FontColor("#9CA3AF");

    private static void ComposeBody(ColumnDescriptor col, PdfBook book, Style theme)
    {
        var chapters = book.Body
            .Where(b => b.Kind == MarkdownBlockKind.Heading && b.Level == 2
                && b.Text.StartsWith("Capítulo", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (chapters.Count > 0)
        {
            ComposeSummary(col, chapters, theme);
        }

        var chapterIndex = 0;
        var dropCapPending = false;

        foreach (var block in book.Body)
        {
            var isChapter = block.Kind == MarkdownBlockKind.Heading && block.Level == 2
                && block.Text.StartsWith("Capítulo", StringComparison.OrdinalIgnoreCase);

            if (isChapter)
            {
                ComposeChapterOpening(col, block.Text, ++chapterIndex, theme);
                dropCapPending = true; // o 1º parágrafo do capítulo recebe capitular
                continue;
            }

            // capitular no 1º parágrafo após a abertura (mesmo que haja uma imagem injetada entre eles)
            if (dropCapPending && block.Kind == MarkdownBlockKind.Paragraph)
            {
                ComposeDropCapParagraph(col, block.Text, theme);
                dropCapPending = false;
                continue;
            }

            ComposeBlock(col, block, theme);
        }

        col.Item().PageBreak();
        ComposeCta(col, book, theme);
    }

    // Sumário visual: ícone + uma linha por capítulo (docs/11 §6), em vez de texto puro.
    private static void ComposeSummary(ColumnDescriptor col, List<MarkdownBlock> chapters, Style theme)
    {
        col.Item().Text("Sumário").FontFamily(theme.HeadingFont).FontSize(24).Bold().FontColor(theme.Primary);
        col.Item().PaddingTop(3).Width(48).Height(3).Background(theme.Accent);
        col.Item().PaddingBottom(12);

        var icon = IconRegistry.Colored("target", theme.Accent);
        foreach (var chapter in chapters)
        {
            var (eyebrow, title) = SplitChapter(chapter.Text);
            var label = title.Length > 0 ? title : eyebrow;
            col.Item().PaddingVertical(3).Row(row =>
            {
                if (icon is not null)
                {
                    row.ConstantItem(15).AlignMiddle().Svg(icon);
                    row.ConstantItem(9);
                }

                row.RelativeItem().AlignMiddle().Text(label).FontSize(12).FontColor(theme.Text);
            });
        }
    }

    // Abertura de capítulo decorativa: número grande (display, tom suave do accent) + ícone + rótulo +
    // título + régua accent, em página própria (docs/12 Parte 1). Eleva a percepção de qualidade.
    private static void ComposeChapterOpening(ColumnDescriptor col, string text, int index, Style theme)
    {
        col.Item().PageBreak();

        var (eyebrow, title) = SplitChapter(text);
        var hasTitle = title.Length > 0;
        var eyebrowText = (hasTitle ? eyebrow : $"Capítulo {index}").ToUpperInvariant();
        var titleText = hasTitle ? title : eyebrow;

        col.Item().PaddingTop(72).PaddingBottom(26).Row(row =>
        {
            row.ConstantItem(120).AlignTop().Text(ChapterNumber(eyebrow, index))
                .FontFamily(theme.HeadingFont).FontSize(80).Bold().FontColor(Tint(theme.Accent, 0.45f));

            row.RelativeItem().PaddingLeft(10).PaddingTop(8).Column(open =>
            {
                var icon = IconRegistry.Colored("sparkles", theme.Accent);
                if (icon is not null)
                {
                    open.Item().Width(26).Svg(icon);
                    open.Item().PaddingTop(10);
                }

                open.Item().Text(eyebrowText)
                    .FontFamily(theme.HeadingFont).FontSize(11).Bold().FontColor(theme.Accent);
                open.Item().PaddingTop(6).Text(titleText)
                    .FontFamily(theme.HeadingFont).FontSize(28).Bold().FontColor(theme.Primary).LineHeight(1.1f);
                open.Item().PaddingTop(16).Width(60).Height(4).Background(theme.Accent);
            });
        });
    }

    // Capitular (drop cap) emulada: 1ª letra grande no accent + restante do parágrafo ao lado (docs/12).
    private static void ComposeDropCapParagraph(ColumnDescriptor col, string text, Style theme)
    {
        if (text.Length < 2)
        {
            col.Item().PaddingBottom(8).Text(text).Justify();
            return;
        }

        col.Item().PaddingBottom(8).Row(row =>
        {
            row.ConstantItem(42).Text(text[..1])
                .FontFamily(theme.HeadingFont).FontSize(46).Bold().FontColor(theme.Accent);
            row.RelativeItem().PaddingTop(10).Text(text[1..]).Justify();
        });
    }

    // Linha do tempo: passos numerados (chip accent) ligados por um conector vertical (docs/13 WS-D).
    private static void ComposeTimeline(ColumnDescriptor col, IReadOnlyList<string> steps, Style theme)
    {
        col.Item().PaddingVertical(10).Column(tl =>
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var (title, body) = SplitStep(steps[i]);
                var isLast = i == steps.Count - 1;
                tl.Item().Row(row =>
                {
                    row.ConstantItem(28).Column(mark =>
                    {
                        mark.Item().Width(24).Height(24).Background(theme.Accent)
                            .AlignCenter().AlignMiddle()
                            .Text((i + 1).ToString()).FontColor("#FFFFFF").Bold().FontSize(11);
                        if (!isLast)
                        {
                            mark.Item().AlignCenter().PaddingTop(2).Width(2).Height(22)
                                .Background(Tint(theme.Accent, 0.4f));
                        }
                    });

                    row.ConstantItem(10);
                    row.RelativeItem().PaddingBottom(isLast ? 0 : 12).Column(c =>
                    {
                        if (title.Length > 0)
                        {
                            c.Item().Text(title)
                                .FontFamily(theme.HeadingFont).FontSize(13).Bold().FontColor(theme.Primary);
                            c.Item().PaddingTop(2);
                        }

                        c.Item().Text(body).FontColor(theme.Text).LineHeight(1.4f);
                    });
                });
            }
        });
    }

    // "Título: descrição" (título curto) → (título, descrição); senão → ("", passo inteiro).
    private static (string Title, string Body) SplitStep(string step)
    {
        var idx = step.IndexOf(": ", StringComparison.Ordinal);
        return idx is > 0 and <= 32
            ? (step[..idx].Trim(), step[(idx + 2)..].Trim())
            : (string.Empty, step);
    }

    // "Capítulo 1 — Título" → ("Capítulo 1", "Título"); sem separador → (texto, "").
    private static (string Eyebrow, string Title) SplitChapter(string text)
    {
        string[] seps = [" — ", " – ", " - ", ": "];
        foreach (var sep in seps)
        {
            var idx = text.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                return (text[..idx].Trim(), text[(idx + sep.Length)..].Trim());
            }
        }

        return (text.Trim(), string.Empty);
    }

    // número do capítulo a partir do rótulo ("Capítulo 1" → "01"); sem dígito, usa a posição.
    private static string ChapterNumber(string eyebrow, int fallback)
    {
        var digits = new string([.. eyebrow.Where(char.IsDigit)]);
        return int.TryParse(digits, out var k) && k > 0 ? k.ToString("00") : fallback.ToString("00");
    }

    // Mistura a cor com branco (towardWhite 0..1): número grande do capítulo em tom suave do accent.
    private static string Tint(string hex, float towardWhite)
    {
        if (hex.Length != 7 || hex[0] != '#')
        {
            return hex;
        }

        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        r = (int)(r + (255 - r) * towardWhite);
        g = (int)(g + (255 - g) * towardWhite);
        b = (int)(b + (255 - b) * towardWhite);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static string Shorten(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";

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

            case MarkdownBlockKind.Timeline:
                ComposeTimeline(col, block.Items, theme);
                break;

            case MarkdownBlockKind.Stat:
                // número de impacto: dígito grande (display) + descrição, sobre card claro do accent
                col.Item().PaddingVertical(10).Background(Tint(theme.Accent, 0.86f)).Padding(16).Row(row =>
                {
                    row.AutoItem().AlignMiddle().Text(block.Text)
                        .FontFamily(theme.HeadingFont).FontSize(34).Bold().FontColor(theme.Primary);
                    if (block.Label.Length > 0)
                    {
                        row.ConstantItem(14);
                        row.RelativeItem().AlignMiddle().Text(block.Label)
                            .FontColor(theme.Text).FontSize(12).LineHeight(1.4f);
                    }
                });
                break;

            case MarkdownBlockKind.QuoteCard:
                // citação desenhada: ícone de aspas + frase em itálico + atribuição (caixa de "voz")
                col.Item().PaddingVertical(12).Background("#F5F5F3").BorderLeft(3).BorderColor(theme.Accent)
                    .Padding(18).Column(q =>
                    {
                        var quoteIcon = IconRegistry.Colored("quote", theme.Accent);
                        if (quoteIcon is not null)
                        {
                            q.Item().Width(20).Svg(quoteIcon);
                            q.Item().PaddingTop(8);
                        }

                        q.Item().Text(block.Text)
                            .FontFamily(theme.HeadingFont).FontSize(15).Italic().FontColor(theme.Primary).LineHeight(1.4f);
                        if (block.Label.Length > 0)
                        {
                            q.Item().PaddingTop(8).Text($"— {block.Label}")
                                .FontFamily(theme.BodyFont).FontSize(11).Bold().FontColor(theme.Accent);
                        }
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
