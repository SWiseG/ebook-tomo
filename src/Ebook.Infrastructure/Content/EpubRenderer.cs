using System.IO.Compression;
using System.Net;
using System.Text;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;

namespace Ebook.Infrastructure.Content;

/// <summary>
/// Exporta um <see cref="PdfBook"/> para EPUB 3 válido usando System.IO.Compression.
/// Estrutura: mimetype (sem compressão, primeiro) → META-INF/container.xml → EPUB/content.opf
/// → EPUB/nav.xhtml → EPUB/style.css → EPUB/cover.xhtml → EPUB/chapters/ch-NNN.xhtml → imagens.
/// </summary>
public sealed class EpubRenderer : IEbookExporter
{
    public byte[] ExportEpub(PdfBook book, byte[]? coverImage = null)
    {
        var uid = Guid.NewGuid().ToString("D");
        var hasCover = coverImage is { Length: > 0 };

        // Coleta imagens inline do corpo
        var inlineImages = new List<(string Id, string FileName, byte[] Bytes)>();
        var bodyWithIds = new List<(MarkdownBlock Block, string? ImgId)>();
        var imgCounter = 0;
        foreach (var block in book.Body)
        {
            if (block.Kind == MarkdownBlockKind.Image && block.ImageBytes is { Length: > 0 })
            {
                imgCounter++;
                var id = $"img-{imgCounter:D3}";
                inlineImages.Add((id, $"images/{id}.png", block.ImageBytes));
                bodyWithIds.Add((block, id));
            }
            else
            {
                bodyWithIds.Add((block, null));
            }
        }

        var chapters = SplitChapters(bodyWithIds);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. mimetype — primeiro entry, sem compressão (requisito EPUB)
            var mimetypeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var w = OpenWriter(mimetypeEntry))
            {
                w.Write("application/epub+zip");
            }

            // 2. META-INF/container.xml
            AddText(zip, "META-INF/container.xml", BuildContainerXml());

            // 3. OPF
            AddText(zip, "EPUB/content.opf",
                BuildOpf(uid, book, chapters.Count, inlineImages, hasCover));

            // 4. nav.xhtml
            AddText(zip, "EPUB/nav.xhtml", BuildNav(book, chapters));

            // 5. style.css
            AddText(zip, "EPUB/style.css", BuildCss(book.Palette));

            // 6. cover.xhtml (se houver capa)
            if (hasCover)
            {
                AddText(zip, "EPUB/cover.xhtml", BuildCoverXhtml());
                AddBytes(zip, "EPUB/images/cover.png", coverImage!);
            }

            // 7. Capítulos
            for (var i = 0; i < chapters.Count; i++)
            {
                AddText(zip, $"EPUB/chapters/ch-{i + 1:D3}.xhtml",
                    BuildChapterXhtml(chapters[i], book.Palette));
            }

            // 8. Imagens inline
            foreach (var (_, fileName, bytes) in inlineImages)
            {
                AddBytes(zip, $"EPUB/{fileName}", bytes);
            }
        }

        return ms.ToArray();
    }

    // Divide a lista de blocos em capítulos por H2 (ou coloca tudo em um capítulo).
    private static List<List<(MarkdownBlock Block, string? ImgId)>> SplitChapters(
        List<(MarkdownBlock Block, string? ImgId)> body)
    {
        var chapters = new List<List<(MarkdownBlock, string?)>>();
        var current = new List<(MarkdownBlock, string?)>();

        foreach (var item in body)
        {
            if (item.Block.Kind == MarkdownBlockKind.Heading && item.Block.Level == 2 && current.Count > 0)
            {
                chapters.Add(current);
                current = new List<(MarkdownBlock, string?)>();
            }

            current.Add(item);
        }

        if (current.Count > 0)
        {
            chapters.Add(current);
        }

        if (chapters.Count == 0)
        {
            chapters.Add(new List<(MarkdownBlock, string?)>());
        }

        return chapters;
    }

    private static string BuildContainerXml() =>
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles>
            <rootfile full-path="EPUB/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    private static string BuildOpf(
        string uid,
        PdfBook book,
        int chapterCount,
        List<(string Id, string FileName, byte[] Bytes)> images,
        bool hasCover)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">""");

        // metadata
        sb.AppendLine("  <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">");
        sb.AppendLine($"    <dc:identifier id=\"uid\">urn:uuid:{uid}</dc:identifier>");
        sb.AppendLine($"    <dc:title>{Esc(book.Title)}</dc:title>");
        sb.AppendLine("    <dc:language>pt-BR</dc:language>");
        sb.AppendLine($"    <meta property=\"dcterms:modified\">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</meta>");
        if (hasCover)
        {
            sb.AppendLine("    <meta name=\"cover\" content=\"cover-img\"/>");
        }

        sb.AppendLine("  </metadata>");

        // manifest
        sb.AppendLine("  <manifest>");
        sb.AppendLine("    <item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>");
        sb.AppendLine("    <item id=\"css\" href=\"style.css\" media-type=\"text/css\"/>");

        if (hasCover)
        {
            sb.AppendLine("    <item id=\"cover-img\" href=\"images/cover.png\" media-type=\"image/png\" properties=\"cover-image\"/>");
            sb.AppendLine("    <item id=\"cover-xhtml\" href=\"cover.xhtml\" media-type=\"application/xhtml+xml\"/>");
        }

        for (var i = 1; i <= chapterCount; i++)
        {
            sb.AppendLine($"    <item id=\"ch-{i:D3}\" href=\"chapters/ch-{i:D3}.xhtml\" media-type=\"application/xhtml+xml\"/>");
        }

        foreach (var (id, fileName, _) in images)
        {
            sb.AppendLine($"    <item id=\"{id}\" href=\"{fileName}\" media-type=\"image/png\"/>");
        }

        sb.AppendLine("  </manifest>");

        // spine
        sb.AppendLine("  <spine>");
        if (hasCover)
        {
            sb.AppendLine("    <itemref idref=\"cover-xhtml\"/>");
        }

        for (var i = 1; i <= chapterCount; i++)
        {
            sb.AppendLine($"    <itemref idref=\"ch-{i:D3}\"/>");
        }

        sb.AppendLine("  </spine>");
        sb.AppendLine("</package>");

        return sb.ToString();
    }

    private static string BuildNav(PdfBook book, List<List<(MarkdownBlock Block, string? ImgId)>> chapters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" lang="pt-BR">
            <head><meta charset="UTF-8"/><title>Navegação</title></head>
            <body>
            <nav epub:type="toc" id="toc">
            <h1>Índice</h1>
            <ol>
            """);

        for (var i = 0; i < chapters.Count; i++)
        {
            var heading = chapters[i].FirstOrDefault(x => x.Block.Kind == MarkdownBlockKind.Heading);
            var label = heading != default ? Esc(heading.Block.Text) : $"Capítulo {i + 1}";
            sb.AppendLine($"  <li><a href=\"chapters/ch-{i + 1:D3}.xhtml\">{label}</a></li>");
        }

        sb.AppendLine("</ol></nav></body></html>");
        return sb.ToString();
    }

    private static string BuildCss(NichePalette? palette)
    {
        var bg = palette?.Background ?? "#FFFFFF";
        var accent = palette?.Accent ?? "#1A73E8";
        var onDark = palette?.OnDark ?? "#FFFFFF";
        var heading = palette?.HeadingFont ?? "serif";
        var body = palette?.BodyFont ?? "sans-serif";

        var sb = new StringBuilder();
        sb.AppendLine("@charset \"UTF-8\";");
        sb.AppendLine($"body {{ font-family: \"{body}\", sans-serif; font-size: 1em; line-height: 1.5; color: #222222; background-color: {bg}; margin: 1em 1.5em; }}");
        sb.AppendLine($"h1, h2, h3 {{ font-family: \"{heading}\", serif; color: {accent}; line-height: 1.2; margin-top: 1.5em; margin-bottom: 0.5em; }}");
        sb.AppendLine("h1 { font-size: 2em; }");
        sb.AppendLine("h2 { font-size: 1.5em; }");
        sb.AppendLine("h3 { font-size: 1.2em; }");
        sb.AppendLine("p { margin: 0.6em 0; text-align: justify; }");
        sb.AppendLine("ul, ol { padding-left: 1.5em; }");
        sb.AppendLine("li { margin: 0.3em 0; }");
        sb.AppendLine($"blockquote {{ border-left: 4px solid {accent}; margin: 1em 0; padding: 0.5em 1em; font-style: italic; color: #555555; }}");
        sb.AppendLine($"aside.callout {{ background-color: {accent}; color: {onDark}; border-radius: 4px; padding: 0.75em 1em; margin: 1em 0; }}");
        sb.AppendLine("aside.callout strong { display: block; font-size: 0.85em; text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 0.3em; }");
        sb.AppendLine(".stat { text-align: center; margin: 1em 0; }");
        sb.AppendLine($".stat .number {{ font-size: 2.5em; font-weight: bold; color: {accent}; display: block; }}");
        sb.AppendLine(".stat .label { font-size: 0.9em; color: #555555; }");
        sb.AppendLine(".quote-card { text-align: center; font-style: italic; }");
        sb.AppendLine($".quote-card .author {{ font-style: normal; font-size: 0.85em; color: {accent}; display: block; margin-top: 0.5em; }}");
        sb.AppendLine("img { max-width: 100%; height: auto; display: block; margin: 1em auto; }");
        sb.AppendLine($"hr {{ border: none; border-top: 2px solid {accent}; margin: 1.5em 0; opacity: 0.4; }}");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 1em 0; }");
        sb.AppendLine("th, td { border: 1px solid #DDDDDD; padding: 0.5em; text-align: left; font-size: 0.9em; }");
        sb.AppendLine($"th {{ background-color: {accent}; color: {onDark}; }}");
        return sb.ToString();
    }

    private static string BuildCoverXhtml() =>
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR">
        <head><meta charset="UTF-8"/><title>Capa</title>
        <link rel="stylesheet" type="text/css" href="../style.css"/>
        </head>
        <body>
        <div style="text-align:center;">
          <img src="../images/cover.png" alt="Capa"/>
        </div>
        </body>
        </html>
        """;

    private static string BuildChapterXhtml(
        List<(MarkdownBlock Block, string? ImgId)> items,
        NichePalette? palette)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR">
            <head><meta charset="UTF-8"/><title>Capítulo</title>
            <link rel="stylesheet" type="text/css" href="../style.css"/>
            </head>
            <body>
            """);

        foreach (var (block, imgId) in items)
        {
            AppendBlock(sb, block, imgId);
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendBlock(StringBuilder sb, MarkdownBlock block, string? imgId)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                var tag = block.Level switch { 1 => "h1", 2 => "h2", _ => "h3" };
                sb.AppendLine($"<{tag}>{Esc(block.Text)}</{tag}>");
                break;

            case MarkdownBlockKind.Paragraph:
                sb.AppendLine($"<p>{Esc(block.Text)}</p>");
                break;

            case MarkdownBlockKind.Bullets:
                sb.AppendLine("<ul>");
                foreach (var item in block.Items)
                {
                    sb.AppendLine($"  <li>{Esc(item)}</li>");
                }

                sb.AppendLine("</ul>");
                break;

            case MarkdownBlockKind.Timeline:
                sb.AppendLine("<ol>");
                foreach (var item in block.Items)
                {
                    sb.AppendLine($"  <li>{Esc(item)}</li>");
                }

                sb.AppendLine("</ol>");
                break;

            case MarkdownBlockKind.PullQuote:
                sb.AppendLine($"<blockquote><p>{Esc(block.Text)}</p></blockquote>");
                break;

            case MarkdownBlockKind.Callout:
                sb.AppendLine("<aside class=\"callout\">");
                if (!string.IsNullOrWhiteSpace(block.Label))
                {
                    sb.AppendLine($"  <strong>{Esc(block.Label)}</strong>");
                }

                sb.AppendLine($"  <p>{Esc(block.Text)}</p>");
                sb.AppendLine("</aside>");
                break;

            case MarkdownBlockKind.Stat:
                sb.AppendLine("<div class=\"stat\">");
                sb.AppendLine($"  <span class=\"number\">{Esc(block.Text)}</span>");
                if (!string.IsNullOrWhiteSpace(block.Label))
                {
                    sb.AppendLine($"  <span class=\"label\">{Esc(block.Label)}</span>");
                }

                sb.AppendLine("</div>");
                break;

            case MarkdownBlockKind.QuoteCard:
                sb.AppendLine("<blockquote class=\"quote-card\">");
                sb.AppendLine($"  <p>{Esc(block.Text)}</p>");
                if (!string.IsNullOrWhiteSpace(block.Label))
                {
                    sb.AppendLine($"  <span class=\"author\">— {Esc(block.Label)}</span>");
                }

                sb.AppendLine("</blockquote>");
                break;

            case MarkdownBlockKind.Comparison:
                var headers = block.Text.Split('|');
                sb.AppendLine("<table><thead><tr>");
                foreach (var h in headers)
                {
                    sb.AppendLine($"  <th>{Esc(h.Trim())}</th>");
                }

                sb.AppendLine("</tr></thead><tbody>");
                foreach (var row in block.Items)
                {
                    var cells = row.Split('|');
                    sb.AppendLine("  <tr>");
                    foreach (var cell in cells)
                    {
                        sb.AppendLine($"    <td>{Esc(cell.Trim())}</td>");
                    }

                    sb.AppendLine("  </tr>");
                }

                sb.AppendLine("</tbody></table>");
                break;

            case MarkdownBlockKind.Divider:
                sb.AppendLine("<hr/>");
                break;

            case MarkdownBlockKind.Image when imgId is not null:
                sb.AppendLine($"<img src=\"../images/{imgId}.png\" alt=\"Ilustração\"/>");
                break;

            case MarkdownBlockKind.Infographic:
                // Infographics sem imagem pré-composta: renderiza como lista de métricas
                sb.AppendLine("<div class=\"stat\">");
                foreach (var cell in block.Items)
                {
                    var sep = cell.IndexOf('|');
                    if (sep > 0)
                    {
                        sb.AppendLine($"  <span class=\"number\">{Esc(cell[..sep].Trim())}</span>");
                        sb.AppendLine($"  <span class=\"label\">{Esc(cell[(sep + 1)..].Trim())}</span>");
                    }
                }

                sb.AppendLine("</div>");
                break;
        }
    }

    private static string Esc(string text) => WebUtility.HtmlEncode(text);

    private static StreamWriter OpenWriter(ZipArchiveEntry entry) =>
        new(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static void AddText(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var w = OpenWriter(entry);
        w.Write(content);
    }

    private static void AddBytes(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(bytes, 0, bytes.Length);
    }
}
