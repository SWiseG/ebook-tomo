using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;

namespace Ebook.Infrastructure.Content;

/// <summary>
/// Exporta um <see cref="PdfBook"/> para DOCX (OpenXML) usando DocumentFormat.OpenXml.
/// Todos os tipos de bloco são mapeados; blocos não suportados caem em parágrafo simples.
/// A capa (coverImage) ocupa a primeira página como imagem de largura total.
/// As fontes primária/secundária da <see cref="NichePalette"/> são aplicadas via estilos nomeados.
/// </summary>
public sealed class DocxRenderer : IDocxExporter
{
    // EMU = English Metric Units; 914400 EMU = 1 polegada; página A4 útil ≈ 5,83 pol = 5.328.240 EMU
    private const long PageWidthEmu = 5_328_240L;
    private const string CalloutFill = "E8E8E8"; // cinza claro para background de Callout

    public byte[] ExportDocx(PdfBook book, byte[]? coverImage = null)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());

            AddStyles(main, book.Palette);
            AddNumbering(main);

            var body = main.Document.Body!;
            var imgCounter = 0;

            // Capa: primeira página com a imagem em largura total
            if (coverImage is { Length: > 0 })
            {
                AppendCoverImage(main, body, coverImage, ref imgCounter);
                AppendPageBreak(body);
            }

            // Título e subtítulo
            body.AppendChild(MakeParagraph(book.Title, "Heading1", book.Palette?.HeadingFont));
            if (!string.IsNullOrWhiteSpace(book.Subtitle))
            {
                body.AppendChild(MakeParagraph(book.Subtitle, "Heading2", book.Palette?.HeadingFont));
            }

            // Corpo
            foreach (var block in book.Body)
            {
                AppendBlock(main, body, block, book.Palette, ref imgCounter);
            }

            // Garante parágrafo final vazio (requisito OpenXML)
            body.AppendChild(new Paragraph());

            main.Document.Save();
        }

        return ms.ToArray();
    }

    // ─── Estilos ─────────────────────────────────────────────────────────────

    private static void AddStyles(MainDocumentPart main, NichePalette? palette)
    {
        var headingFont = palette?.HeadingFont ?? "Calibri";
        var bodyFont = palette?.BodyFont ?? "Calibri";
        var accent = HexColor(palette?.Accent ?? "#1A73E8");

        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles(
            MakeStyle("Normal", "Normal", bodyFont, "240", null, false, false),
            MakeStyle("Heading1", "Heading1", headingFont, "360", accent, true, false),
            MakeStyle("Heading2", "Heading2", headingFont, "320", accent, true, false),
            MakeStyle("Heading3", "Heading3", headingFont, "280", accent, true, false),
            MakeStyle("Quote", "Quote", bodyFont, "240", null, false, true),
            MakeStyle("Caption", "Caption", bodyFont, "200", null, false, true)
        );
    }

    private static Style MakeStyle(
        string styleId, string name, string font, string halfPoints,
        string? hexColor, bool bold, bool italic)
    {
        var rPr = new StyleRunProperties();
        rPr.AppendChild(new RunFonts { Ascii = font, HighAnsi = font });
        rPr.AppendChild(new FontSize { Val = halfPoints });
        if (bold) rPr.AppendChild(new Bold());
        if (italic) rPr.AppendChild(new Italic());
        if (hexColor is not null)
        {
            rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = hexColor });
        }

        var pPr = new StyleParagraphProperties();
        pPr.AppendChild(new SpacingBetweenLines { After = "120", Line = "276", LineRule = LineSpacingRuleValues.Auto });

        var style = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId,
        };
        style.AppendChild(new StyleName { Val = name });
        if (styleId != "Normal")
        {
            style.AppendChild(new BasedOn { Val = "Normal" });
        }

        style.AppendChild(pPr);
        style.AppendChild(rPr);
        return style;
    }

    // ─── Numeração (para listas) ──────────────────────────────────────────────

    private static void AddNumbering(MainDocumentPart main)
    {
        var numPart = main.AddNewPart<NumberingDefinitionsPart>();

        // AbstractNum 1: bullets
        var abstractNumBullet = new AbstractNum(
            new Level(
                new NumberingFormat { Val = NumberFormatValues.Bullet },
                new LevelText { Val = "•" },
                new Indentation { Left = "720", Hanging = "360" }
            )
            { LevelIndex = 0 })
        { AbstractNumberId = 1 };

        // AbstractNum 2: decimal (timeline)
        var abstractNumDecimal = new AbstractNum(
            new Level(
                new NumberingFormat { Val = NumberFormatValues.Decimal },
                new LevelText { Val = "%1." },
                new Indentation { Left = "720", Hanging = "360" }
            )
            { LevelIndex = 0 })
        { AbstractNumberId = 2 };

        var numBullet = new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 };
        var numDecimal = new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = 2 };

        numPart.Numbering = new Numbering(abstractNumBullet, abstractNumDecimal, numBullet, numDecimal);
    }

    // ─── Mapeamento de blocos ─────────────────────────────────────────────────

    private static void AppendBlock(
        MainDocumentPart main, Body body,
        MarkdownBlock block, NichePalette? palette,
        ref int imgCounter)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                var styleId = block.Level switch { 1 => "Heading1", 2 => "Heading2", _ => "Heading3" };
                body.AppendChild(MakeParagraph(block.Text, styleId, palette?.HeadingFont));
                break;

            case MarkdownBlockKind.Paragraph:
                body.AppendChild(MakeParagraph(block.Text, "Normal", palette?.BodyFont));
                break;

            case MarkdownBlockKind.Bullets:
                foreach (var item in block.Items)
                {
                    body.AppendChild(MakeListParagraph(item, numberId: 1, palette?.BodyFont));
                }

                break;

            case MarkdownBlockKind.Timeline:
                foreach (var item in block.Items)
                {
                    body.AppendChild(MakeListParagraph(item, numberId: 2, palette?.BodyFont));
                }

                break;

            case MarkdownBlockKind.PullQuote:
                body.AppendChild(MakeQuoteParagraph(block.Text, palette));
                break;

            case MarkdownBlockKind.Callout:
                body.AppendChild(MakeCalloutTable(block.Label, block.Text, palette));
                break;

            case MarkdownBlockKind.Stat:
                body.AppendChild(MakeStatParagraph(block.Text, block.Label, palette));
                break;

            case MarkdownBlockKind.QuoteCard:
                body.AppendChild(MakeQuoteParagraph(block.Text, palette));
                if (!string.IsNullOrWhiteSpace(block.Label))
                {
                    body.AppendChild(MakeParagraph($"— {block.Label}", "Caption", palette?.BodyFont));
                }

                break;

            case MarkdownBlockKind.Comparison:
                body.AppendChild(MakeComparisonTable(block.Text, block.Items, palette));
                break;

            case MarkdownBlockKind.Divider:
                body.AppendChild(MakeDivider(palette));
                break;

            case MarkdownBlockKind.Image when block.ImageBytes is { Length: > 0 }:
                AppendInlineImage(main, body, block.ImageBytes, ref imgCounter);
                break;

            case MarkdownBlockKind.Image:
                // Sem bytes → placeholder
                body.AppendChild(MakeParagraph("[imagem]", "Normal", palette?.BodyFont));
                break;

            case MarkdownBlockKind.Infographic:
                foreach (var cell in block.Items)
                {
                    var sep = cell.IndexOf('|');
                    var text = sep > 0
                        ? $"{cell[..sep].Trim()} — {cell[(sep + 1)..].Trim()}"
                        : cell;
                    body.AppendChild(MakeParagraph(text, "Normal", palette?.BodyFont));
                }

                break;

            default:
                // Bloco não mapeado → parágrafo simples (modo seguro)
                body.AppendChild(MakeParagraph(block.Text, "Normal", palette?.BodyFont));
                break;
        }
    }

    // ─── Helpers de parágrafos ────────────────────────────────────────────────

    private static Paragraph MakeParagraph(string text, string styleId, string? font)
    {
        var pPr = new ParagraphProperties(new ParagraphStyleId { Val = styleId });
        var run = MakeRun(text, font);
        return new Paragraph(pPr, run);
    }

    private static Paragraph MakeListParagraph(string text, int numberId, string? font)
    {
        var pPr = new ParagraphProperties(
            new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numberId }));
        var run = MakeRun(text, font);
        return new Paragraph(pPr, run);
    }

    private static Paragraph MakeQuoteParagraph(string text, NichePalette? palette)
    {
        var pPr = new ParagraphProperties(
            new ParagraphStyleId { Val = "Quote" },
            new Indentation { Left = "720", Right = "720" });
        var rPr = new RunProperties(new Italic());
        if (palette?.Accent is not null)
        {
            rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = HexColor(palette.Accent) });
        }

        var run = new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(pPr, run);
    }

    private static Paragraph MakeStatParagraph(string number, string label, NichePalette? palette)
    {
        var accent = HexColor(palette?.Accent ?? "#1A73E8");
        var pPr = new ParagraphProperties(
            new Justification { Val = JustificationValues.Center });

        var numRPr = new RunProperties(
            new Bold(),
            new FontSize { Val = "480" }, // 24pt
            new DocumentFormat.OpenXml.Wordprocessing.Color { Val = accent });
        var numRun = new Run(numRPr, new Text(number) { Space = SpaceProcessingModeValues.Preserve });

        var para = new Paragraph(pPr, numRun);
        if (!string.IsNullOrWhiteSpace(label))
        {
            var lblRPr = new RunProperties(new FontSize { Val = "200" });
            var lblRun = new Run(lblRPr, new Text($" — {label}") { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(lblRun);
        }

        return para;
    }

    private static Paragraph MakeDivider(NichePalette? palette)
    {
        var accent = HexColor(palette?.Accent ?? "#1A73E8");
        var border = new BottomBorder
        {
            Val = BorderValues.Single,
            Size = 6,
            Color = accent,
        };
        var pPr = new ParagraphProperties(new ParagraphBorders(border));
        return new Paragraph(pPr);
    }

    private static Run MakeRun(string text, string? font)
    {
        var rPr = new RunProperties();
        if (font is not null)
        {
            rPr.AppendChild(new RunFonts { Ascii = font, HighAnsi = font });
        }

        return new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    // ─── Callout como tabela 1×1 com fundo cinza ─────────────────────────────

    private static Table MakeCalloutTable(string label, string text, NichePalette? palette)
    {
        var tblPr = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None }));

        var shading = new Shading { Fill = CalloutFill, Val = ShadingPatternValues.Clear };
        var tcPr = new TableCellProperties(shading,
            new TableCellMargin(
                new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "200", Type = TableWidthUnitValues.Dxa }));

        var font = palette?.BodyFont;
        var cellContent = new List<OpenXmlElement>();
        if (!string.IsNullOrWhiteSpace(label))
        {
            var labelRPr = new RunProperties(new Bold());
            cellContent.Add(new Paragraph(new Run(labelRPr, new Text(label))));
        }

        cellContent.Add(new Paragraph(MakeRun(text, font)));

        var cell = new TableCell(tcPr);
        foreach (var p in cellContent) cell.AppendChild(p);

        var row = new TableRow(cell);
        var table = new Table(tblPr, row);
        return table;
    }

    // ─── Comparison como tabela ───────────────────────────────────────────────

    private static Table MakeComparisonTable(string header, IReadOnlyList<string> rows, NichePalette? palette)
    {
        var accent = HexColor(palette?.Accent ?? "#1A73E8");
        var onDark = HexColor(palette?.OnDark ?? "#FFFFFF");
        var font = palette?.BodyFont;

        var tblPr = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" }));

        var table = new Table(tblPr);

        // Cabeçalho
        var headers = header.Split('|');
        var headerRow = new TableRow();
        foreach (var h in headers)
        {
            var hTcPr = new TableCellProperties(new Shading { Fill = accent, Val = ShadingPatternValues.Clear });
            var hRPr = new RunProperties(new Bold(),
                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = onDark });
            var hRun = new Run(hRPr, new Text(h.Trim()));
            headerRow.AppendChild(new TableCell(hTcPr, new Paragraph(hRun)));
        }

        table.AppendChild(headerRow);

        // Linhas de dados
        foreach (var row in rows)
        {
            var cells = row.Split('|');
            var dataRow = new TableRow();
            foreach (var cell in cells)
            {
                dataRow.AppendChild(new TableCell(new Paragraph(MakeRun(cell.Trim(), font))));
            }

            table.AppendChild(dataRow);
        }

        return table;
    }

    // ─── Imagens ──────────────────────────────────────────────────────────────

    private static void AppendCoverImage(
        MainDocumentPart main, Body body, byte[] imageBytes, ref int imgCounter)
    {
        imgCounter++;
        var part = main.AddImagePart(ImagePartType.Png);
        using var imgMs = new MemoryStream(imageBytes);
        part.FeedData(imgMs);

        var (cx, cy) = FitImageEmu(imageBytes, PageWidthEmu);
        var drawing = BuildDrawing(main.GetIdOfPart(part), cx, cy, $"img{imgCounter}");
        body.AppendChild(new Paragraph(new Run(drawing)));
    }

    private static void AppendInlineImage(
        MainDocumentPart main, Body body, byte[] imageBytes, ref int imgCounter)
    {
        imgCounter++;
        var part = main.AddImagePart(ImagePartType.Png);
        using var imgMs = new MemoryStream(imageBytes);
        part.FeedData(imgMs);

        var (cx, cy) = FitImageEmu(imageBytes, PageWidthEmu);
        var drawing = BuildDrawing(main.GetIdOfPart(part), cx, cy, $"img{imgCounter}");
        body.AppendChild(new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(drawing)));
    }

    private static Drawing BuildDrawing(string relId, long cx, long cy, string name)
    {
        var docPr = new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties
        {
            Id = 1,
            Name = name,
        };
        var extent = new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = cx, Cy = cy };
        var inline = new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
            new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent
            {
                LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0
            },
            extent,
            docPr,
            new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(
                new DocumentFormat.OpenXml.Drawing.GraphicFrameLocks { NoChangeAspect = true }),
            BuildGraphic(relId, cx, cy))
        {
            DistanceFromTop = 0,
            DistanceFromBottom = 0,
            DistanceFromLeft = 0,
            DistanceFromRight = 0,
        };

        return new Drawing(inline);
    }

    private static DocumentFormat.OpenXml.Drawing.Graphic BuildGraphic(string relId, long cx, long cy)
    {
        var pic = new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = 0, Name = "Picture" },
                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
            new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                new DocumentFormat.OpenXml.Drawing.Blip
                {
                    Embed = relId,
                    CompressionState = DocumentFormat.OpenXml.Drawing.BlipCompressionValues.Print,
                },
                new DocumentFormat.OpenXml.Drawing.Stretch(
                    new DocumentFormat.OpenXml.Drawing.FillRectangle())),
            new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                new DocumentFormat.OpenXml.Drawing.Transform2D(
                    new DocumentFormat.OpenXml.Drawing.Offset { X = 0, Y = 0 },
                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = cx, Cy = cy }),
                new DocumentFormat.OpenXml.Drawing.PresetGeometry(
                    new DocumentFormat.OpenXml.Drawing.AdjustValueList())
                { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }));

        return new DocumentFormat.OpenXml.Drawing.Graphic(
            new DocumentFormat.OpenXml.Drawing.GraphicData(pic)
            {
                Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
            });
    }

    /// <summary>Lê as dimensões do PNG do cabeçalho IHDR e calcula EMUs mantendo proporção.</summary>
    private static (long Cx, long Cy) FitImageEmu(byte[] png, long maxWidthEmu)
    {
        int srcW = 800, srcH = 1200; // fallback seguro
        try
        {
            // PNG IHDR: bytes 16-23 contêm width e height como int32 big-endian
            if (png.Length >= 24)
            {
                srcW = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
                srcH = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            }
        }
        catch
        {
            // ignora erro de leitura de cabeçalho — usa fallback
        }

        if (srcW <= 0) srcW = 800;
        if (srcH <= 0) srcH = 1200;

        var cx = maxWidthEmu;
        var cy = (long)(maxWidthEmu * (double)srcH / srcW);
        return (cx, cy);
    }

    // ─── Quebra de página ─────────────────────────────────────────────────────

    private static void AppendPageBreak(Body body)
    {
        body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
    }

    // ─── Utilitários ──────────────────────────────────────────────────────────

    /// <summary>Remove '#' e normaliza cor hex para uso em OpenXML (6 chars sem '#').</summary>
    private static string HexColor(string hex) =>
        hex.TrimStart('#').PadRight(6, '0')[..6].ToUpperInvariant();
}
