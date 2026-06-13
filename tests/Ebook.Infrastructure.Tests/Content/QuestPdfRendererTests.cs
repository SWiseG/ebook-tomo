using System.Text;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Infrastructure.Content;

namespace Ebook.Infrastructure.Tests.Content;

public class QuestPdfRendererTests
{
    private static PdfBook SampleBook(PdfTheme theme) => new(
        Title: "Dinheiro Sob Controle",
        Subtitle: "O guia do autônomo",
        Tagline: "Organize suas finanças em 30 dias",
        Theme: theme,
        Body:
        [
            MarkdownBlock.Paragraph("Introdução envolvente sobre o tema."),
            MarkdownBlock.Heading(2, "Capítulo 1 — Mapeie seu dinheiro"),
            MarkdownBlock.Paragraph("Conteúdo do primeiro capítulo."),
            MarkdownBlock.Heading(3, "Subtópico"),
            MarkdownBlock.Bullets(["receitas", "despesas"]),
            MarkdownBlock.Heading(2, "Capítulo 2 — Crie sua reserva"),
            MarkdownBlock.Paragraph("Conteúdo do segundo capítulo.")
        ],
        Cta: new PdfCta("Garanta seu exemplar", "https://lp/exemplo"));

    [Theory]
    [InlineData(PdfTheme.Classic)]
    [InlineData(PdfTheme.Modern)]
    [InlineData(PdfTheme.Editorial)]
    public void Render_produz_pdf_valido_para_cada_tema(PdfTheme theme)
    {
        var renderer = new QuestPdfRenderer();

        var bytes = renderer.Render(SampleBook(theme));

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, "PDF deveria ter conteúdo substancial");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes[..4]));
    }

    [Fact]
    public void Render_com_capa_de_imagem_embute_a_capa_gerada()
    {
        var composer = new SkiaImageComposer();
        var palette = PaletteCatalog.ForNiche("financas-autonomos");
        var cover = composer.RenderCover(new CoverArt("Dinheiro Sob Controle", "O guia do autônomo", null, palette));

        var bytes = new QuestPdfRenderer().Render(SampleBook(PdfTheme.Classic), cover);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes[..4]));
        Assert.True(bytes.Length > cover.Length, "PDF com capa embutida deveria ser maior que a própria capa");
    }
}
