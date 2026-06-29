using System.IO.Compression;
using System.Text;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Infrastructure.Content;

namespace Ebook.Infrastructure.Tests.Content;

/// <summary>
/// Testes unitários do DocxRenderer: verifica estrutura OpenXML (ZIP) sem rede, IA ou CLI.
/// </summary>
public class DocxRendererTests
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
            MarkdownBlock.PullQuote("Citação de destaque."),
            MarkdownBlock.Callout("Insight rápido", "Dica importante aqui."),
        };
        body.AddRange(extraBlocks);

        return new PdfBook(
            Title: "Guia de Testes DOCX",
            Subtitle: "Subtítulo",
            Tagline: "Tagline do e-book",
            Theme: PdfTheme.Classic,
            Body: body,
            Cta: new PdfCta("Compre agora", "https://example.com/checkout"),
            Palette: TestPalette);
    }

    // PNG mínimo válido (IHDR): 800×600
    private static byte[] FakePng800x600()
    {
        var png = new byte[33];
        // Assinatura PNG
        png[0] = 0x89; png[1] = 0x50; png[2] = 0x4E; png[3] = 0x47;
        png[4] = 0x0D; png[5] = 0x0A; png[6] = 0x1A; png[7] = 0x0A;
        // Chunk length IHDR = 13
        png[8] = 0x00; png[9] = 0x00; png[10] = 0x00; png[11] = 0x0D;
        // Tipo IHDR
        png[12] = 0x49; png[13] = 0x48; png[14] = 0x44; png[15] = 0x52;
        // Width = 800 (0x00000320)
        png[16] = 0x00; png[17] = 0x00; png[18] = 0x03; png[19] = 0x20;
        // Height = 600 (0x00000258)
        png[20] = 0x00; png[21] = 0x00; png[22] = 0x02; png[23] = 0x58;
        // bit depth, color type, etc. (dummy)
        png[24] = 8; png[25] = 2;
        return png;
    }

    private static ZipArchive ExportZip(PdfBook book, byte[]? cover = null)
    {
        var renderer = new DocxRenderer();
        var bytes = renderer.ExportDocx(book, cover);
        return new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
    }

    [Fact]
    public void Document_xml_existe_e_nao_esta_vazio()
    {
        using var zip = ExportZip(BuildBook());

        var entry = zip.GetEntry("word/document.xml");
        Assert.NotNull(entry);
        Assert.True(entry!.Length > 0);
    }

    [Fact]
    public void Styles_xml_existe()
    {
        using var zip = ExportZip(BuildBook());

        var entry = zip.GetEntry("word/styles.xml");
        Assert.NotNull(entry);
    }

    [Fact]
    public void Document_xml_contem_titulo_do_livro()
    {
        using var zip = ExportZip(BuildBook());

        var entry = zip.GetEntry("word/document.xml")!;
        using var r = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = r.ReadToEnd();
        Assert.Contains("Guia de Testes DOCX", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Com_capa_existe_imagem_no_pacote()
    {
        // OpenXML SDK 3.x gera URIs de imagens no nível "media/" (fora de word/),
        // mas o arquivo é válido para leitores OOXML. Verificamos a presença da imagem em qualquer path.
        using var zip = ExportZip(BuildBook(), FakePng800x600());

        var imageEntries = zip.Entries
            .Where(e => e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                     || e.FullName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(imageEntries.Count >= 1,
            $"Deve haver ≥1 imagem no pacote. Entries: [{string.Join(", ", zip.Entries.Select(e => e.FullName))}]");
    }

    [Fact]
    public void Com_imagem_inline_existe_imagem_no_pacote()
    {
        var book = BuildBook(MarkdownBlock.Image(FakePng800x600()));
        using var zip = ExportZip(book);

        var imageEntries = zip.Entries
            .Where(e => e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                     || e.FullName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(imageEntries.Count >= 1,
            $"Deve haver ≥1 imagem no pacote. Entries: [{string.Join(", ", zip.Entries.Select(e => e.FullName))}]");
    }

    [Fact]
    public void Bloco_Image_sem_bytes_gera_placeholder_nao_lanca_excecao()
    {
        var blockSemBytes = new MarkdownBlock { Kind = MarkdownBlockKind.Image };
        var book = BuildBook(blockSemBytes);

        // Não deve lançar exceção
        using var zip = ExportZip(book);

        var entry = zip.GetEntry("word/document.xml")!;
        using var r = new StreamReader(entry.Open(), Encoding.UTF8);
        Assert.Contains("[imagem]", r.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Bloco_nao_suportado_cai_em_paragrafo_simples_sem_excecao()
    {
        // Simula bloco com Kind=Divider (usa Text vazio por padrão — testamos que não lança)
        var book = BuildBook(
            MarkdownBlock.Divider(),
            MarkdownBlock.Stat("97%", "de satisfação"),
            MarkdownBlock.QuoteCard("Frase.", "Autor"),
            MarkdownBlock.Comparison("Antes | Depois", ["Ruim | Bom"]),
            MarkdownBlock.Infographic(["97% | satisfação", "3x | crescimento"]));

        using var zip = ExportZip(book);
        var entry = zip.GetEntry("word/document.xml")!;
        Assert.NotNull(entry);
        Assert.True(entry.Length > 0);
    }

    [Fact]
    public void Styles_xml_contem_fontes_da_paleta()
    {
        using var zip = ExportZip(BuildBook());

        var entry = zip.GetEntry("word/styles.xml")!;
        using var r = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = r.ReadToEnd();
        Assert.Contains("Inter", xml, StringComparison.Ordinal);  // HeadingFont
        Assert.Contains("Lora", xml, StringComparison.Ordinal);   // BodyFont
    }

    [Fact]
    public void Todos_os_tipos_de_bloco_nao_lancam_excecao()
    {
        var book = new PdfBook(
            Title: "Teste Completo",
            Subtitle: null,
            Tagline: null,
            Theme: PdfTheme.Classic,
            Body: new List<MarkdownBlock>
            {
                MarkdownBlock.Heading(1, "Título Principal"),
                MarkdownBlock.Heading(2, "Capítulo"),
                MarkdownBlock.Heading(3, "Seção"),
                MarkdownBlock.Paragraph("Parágrafo normal."),
                MarkdownBlock.Bullets(["Item A", "Item B"]),
                MarkdownBlock.Timeline(["Passo 1", "Passo 2"]),
                MarkdownBlock.PullQuote("Citação."),
                MarkdownBlock.Callout("Label", "Conteúdo callout."),
                MarkdownBlock.Stat("97%", "de satisfação"),
                MarkdownBlock.QuoteCard("Frase inspiradora.", "Autor"),
                MarkdownBlock.Comparison("Antes | Depois", ["Ruim | Bom", "Lento | Rápido"]),
                MarkdownBlock.Divider(),
                MarkdownBlock.Image(FakePng800x600()),
                MarkdownBlock.Infographic(["97% | satisfação", "3x | crescimento"]),
            },
            Cta: new PdfCta("CTA", null),
            Palette: TestPalette);

        using var zip = ExportZip(book);
        Assert.NotNull(zip.GetEntry("word/document.xml"));
    }
}
