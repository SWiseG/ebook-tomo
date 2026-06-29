using System.IO.Compression;
using System.Text;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Infrastructure.Content;

namespace Ebook.Infrastructure.Tests.Content;

/// <summary>
/// Testes unitários do EpubRenderer: verifica estrutura EPUB 3 sem rede, IA ou CLI.
/// </summary>
public class EpubRendererTests
{
    private static readonly NichePalette TestPalette = new(
        Background: "#FFFFFF",
        Accent: "#1A73E8",
        OnDark: "#FFFFFF",
        HeadingFont: "Inter",
        BodyFont: "Lora");

    private static PdfBook BuildBook(params MarkdownBlock[] extraBlocks)
    {
        var body = new List<MarkdownBlock>
        {
            MarkdownBlock.Heading(2, "Capítulo 1 — Introdução"),
            MarkdownBlock.Paragraph("Parágrafo de introdução do capítulo."),
            MarkdownBlock.PullQuote("Citação de destaque do capítulo."),
            MarkdownBlock.Callout("Insight rápido", "Dica importante aqui."),
        };
        body.AddRange(extraBlocks);

        return new PdfBook(
            Title: "Guia de Testes",
            Subtitle: "Subtítulo",
            Tagline: "Tagline do e-book",
            Theme: PdfTheme.Classic,
            Body: body,
            Cta: new PdfCta("Compre agora", "https://example.com/checkout"),
            Palette: TestPalette);
    }

    private static byte[] FakePng() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static ZipArchive ExportZip(PdfBook book, byte[]? cover = null)
    {
        var renderer = new EpubRenderer();
        var bytes = renderer.ExportEpub(book, cover);
        return new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
    }

    [Fact]
    public void Mimetype_e_primeiro_entry_e_nao_esta_comprimido()
    {
        using var zip = ExportZip(BuildBook());

        var first = zip.Entries[0];
        Assert.Equal("mimetype", first.FullName);

        // Para NoCompression (stored), o tamanho comprimido == tamanho original
        Assert.Equal(first.Length, first.CompressedLength);

        using var r = new StreamReader(first.Open(), Encoding.UTF8);
        Assert.Equal("application/epub+zip", r.ReadToEnd());
    }

    [Fact]
    public void Container_xml_existe_em_META_INF()
    {
        using var zip = ExportZip(BuildBook());

        var entry = zip.GetEntry("META-INF/container.xml");
        Assert.NotNull(entry);

        using var r = new StreamReader(entry!.Open());
        var xml = r.ReadToEnd();
        Assert.Contains("EPUB/content.opf", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifesto_tem_pelo_menos_um_xhtml()
    {
        using var zip = ExportZip(BuildBook());

        var opf = zip.GetEntry("EPUB/content.opf");
        Assert.NotNull(opf);

        using var r = new StreamReader(opf!.Open());
        var content = r.ReadToEnd();
        Assert.Contains(".xhtml", content, StringComparison.Ordinal);
        Assert.Contains("application/xhtml+xml", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Com_capa_cover_xhtml_esta_no_spine_como_primeiro_item()
    {
        using var zip = ExportZip(BuildBook(), FakePng());

        var opf = zip.GetEntry("EPUB/content.opf");
        Assert.NotNull(opf);

        using var r = new StreamReader(opf!.Open());
        var content = r.ReadToEnd();

        // Extrai apenas o trecho do spine para verificar a ordem
        var spineStart = content.IndexOf("<spine>", StringComparison.Ordinal);
        Assert.True(spineStart >= 0, "OPF deve conter <spine>");
        var spineSection = content[spineStart..];
        var coverInSpine = spineSection.IndexOf("cover-xhtml", StringComparison.Ordinal);
        var ch1InSpine = spineSection.IndexOf("ch-001", StringComparison.Ordinal);
        Assert.True(coverInSpine >= 0, "cover-xhtml deve estar no spine");
        Assert.True(coverInSpine < ch1InSpine, "cover-xhtml deve preceder os capítulos no spine");
    }

    [Fact]
    public void Com_capa_cover_png_incluido_no_zip()
    {
        using var zip = ExportZip(BuildBook(), FakePng());

        var entry = zip.GetEntry("EPUB/images/cover.png");
        Assert.NotNull(entry);
        Assert.True(entry!.Length > 0);
    }

    [Fact]
    public void Sem_capa_nao_ha_cover_xhtml_no_zip()
    {
        using var zip = ExportZip(BuildBook());

        Assert.Null(zip.GetEntry("EPUB/cover.xhtml"));
        Assert.Null(zip.GetEntry("EPUB/images/cover.png"));
    }

    [Fact]
    public void Imagem_inline_incluida_no_zip_e_referenciada_no_manifesto()
    {
        var fakeImg = FakePng();
        var book = BuildBook(MarkdownBlock.Image(fakeImg));

        using var zip = ExportZip(book);

        // a imagem deve estar no ZIP
        var imgEntry = zip.GetEntry("EPUB/images/img-001.png");
        Assert.NotNull(imgEntry);

        // o manifesto deve referenciar a imagem
        var opf = zip.GetEntry("EPUB/content.opf");
        using var r = new StreamReader(opf!.Open());
        Assert.Contains("img-001", r.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Nav_xhtml_existe_com_propriedade_nav()
    {
        using var zip = ExportZip(BuildBook());

        var nav = zip.GetEntry("EPUB/nav.xhtml");
        Assert.NotNull(nav);

        var opf = zip.GetEntry("EPUB/content.opf");
        using var r = new StreamReader(opf!.Open());
        Assert.Contains("properties=\"nav\"", r.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Css_inclui_cores_e_fontes_da_paleta()
    {
        using var zip = ExportZip(BuildBook());

        var css = zip.GetEntry("EPUB/style.css");
        Assert.NotNull(css);

        using var r = new StreamReader(css!.Open());
        var content = r.ReadToEnd();
        Assert.Contains("#1A73E8", content, StringComparison.Ordinal); // Accent
        Assert.Contains("Inter", content, StringComparison.Ordinal);    // HeadingFont
        Assert.Contains("Lora", content, StringComparison.Ordinal);     // BodyFont
        Assert.Contains("line-height: 1.5", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Capitulos_xhtml_mapeiam_todos_os_tipos_de_bloco()
    {
        // Todos os blocos num único capítulo (sem H1 separado para não criar capítulo extra)
        var book = new PdfBook(
            Title: "Teste de Blocos",
            Subtitle: null,
            Tagline: null,
            Theme: PdfTheme.Classic,
            Body: new List<MarkdownBlock>
            {
                MarkdownBlock.Heading(2, "Capítulo 1"),
                MarkdownBlock.Heading(1, "Título Principal"),
                MarkdownBlock.Paragraph("Parágrafo."),
                MarkdownBlock.Bullets(["Item A", "Item B"]),
                MarkdownBlock.Timeline(["Passo 1", "Passo 2"]),
                MarkdownBlock.PullQuote("Citação."),
                MarkdownBlock.Callout("Label", "Conteúdo do callout."),
                MarkdownBlock.Stat("97%", "de satisfação"),
                MarkdownBlock.QuoteCard("Frase inspiradora.", "Autor"),
                MarkdownBlock.Comparison("Antes | Depois", ["Ruim | Bom"]),
                MarkdownBlock.Divider(),
            },
            Cta: new PdfCta("CTA", null),
            Palette: TestPalette);

        using var zip = ExportZip(book);

        // Lê todos os capítulos e concatena o XHTML para verificar os tipos de bloco
        var allXhtml = new StringBuilder();
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("EPUB/chapters/", StringComparison.Ordinal)))
        {
            using var r = new StreamReader(entry.Open());
            allXhtml.Append(r.ReadToEnd());
        }

        var xhtml = allXhtml.ToString();
        Assert.Contains("<h1>", xhtml, StringComparison.Ordinal);
        Assert.Contains("<h2>", xhtml, StringComparison.Ordinal);
        Assert.Contains("<ul>", xhtml, StringComparison.Ordinal);
        Assert.Contains("<ol>", xhtml, StringComparison.Ordinal);
        Assert.Contains("<blockquote>", xhtml, StringComparison.Ordinal);
        Assert.Contains("<aside", xhtml, StringComparison.Ordinal);
        Assert.Contains("class=\"stat\"", xhtml, StringComparison.Ordinal);
        Assert.Contains("<table>", xhtml, StringComparison.Ordinal);
        Assert.Contains("<hr/>", xhtml, StringComparison.Ordinal);
    }
}
