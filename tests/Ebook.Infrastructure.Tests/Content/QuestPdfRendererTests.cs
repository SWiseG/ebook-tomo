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
    public void Render_suporta_blocos_ricos_pullquote_callout_e_checklist()
    {
        var book = SampleBook(PdfTheme.Classic) with
        {
            Body =
            [
                MarkdownBlock.Heading(2, "Capítulo 1 — Comece aqui"),
                MarkdownBlock.Paragraph("Abertura do capítulo."),
                MarkdownBlock.PullQuote("Quem controla o dinheiro controla o futuro."),
                MarkdownBlock.Callout("Insight rápido", "Automatize a poupança no dia do salário."),
                MarkdownBlock.Callout("Estudo de caso", "A Ana saiu do vermelho em 60 dias."),
                MarkdownBlock.Bullets(["[ ] listar dívidas", "[x] abrir conta", "item comum"]),
            ],
        };

        var bytes = new QuestPdfRenderer().Render(book);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes[..4]));
        Assert.True(bytes.Length > 1000);
    }

    // Regressão: a capitular (1ª letra do parágrafo após o capítulo) com letra larga (W/M) estourava
    // o ConstantItem fixo, e um "número" longo no card de Stat estourava o card — ambos geravam
    // "conflicting size constraints" e abortavam a etapa ebook.pdf. Devem renderizar sem lançar.
    [Theory]
    [InlineData(PdfTheme.Classic)]
    [InlineData(PdfTheme.Modern)]
    [InlineData(PdfTheme.Editorial)]
    public void Render_nao_estoura_layout_com_capitular_larga_e_stat_longo(PdfTheme theme)
    {
        var book = SampleBook(theme) with
        {
            Body =
            [
                MarkdownBlock.Heading(2, "Capítulo 1 — Workflows que Multiplicam Resultados"),
                MarkdownBlock.Paragraph("Whatsapp mudou tudo para sempre nos negócios digitais modernos."),
                MarkdownBlock.Stat("87% de todos os profissionais de marketing digital do mundo", "afirmam que conteúdo gera ROI"),
                MarkdownBlock.Stat("R$ 1.000.000", string.Empty),
            ],
        };

        var bytes = new QuestPdfRenderer().Render(book);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes[..4]));
        Assert.True(bytes.Length > 1000);
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
