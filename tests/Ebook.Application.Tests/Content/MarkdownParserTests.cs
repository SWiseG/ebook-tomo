using Ebook.Application.Content.Pdf;

namespace Ebook.Application.Tests.Content;

public class MarkdownParserTests
{
    [Fact]
    public void Parse_classifica_headings_paragrafos_e_bullets()
    {
        const string md = """
            # Título
            ## Subtítulo

            Primeiro parágrafo
            continua na mesma frase.

            - item um
            - item dois

            ### Seção
            """;

        var blocks = MarkdownParser.Parse(md);

        Assert.Collection(blocks,
            b => { Assert.Equal(MarkdownBlockKind.Heading, b.Kind); Assert.Equal(1, b.Level); Assert.Equal("Título", b.Text); },
            b => { Assert.Equal(MarkdownBlockKind.Heading, b.Kind); Assert.Equal(2, b.Level); },
            b => { Assert.Equal(MarkdownBlockKind.Paragraph, b.Kind); Assert.Equal("Primeiro parágrafo continua na mesma frase.", b.Text); },
            b => { Assert.Equal(MarkdownBlockKind.Bullets, b.Kind); Assert.Equal(2, b.Items.Count); },
            b => { Assert.Equal(MarkdownBlockKind.Heading, b.Kind); Assert.Equal(3, b.Level); });
    }

    [Fact]
    public void Parse_remove_marcadores_inline()
    {
        var blocks = MarkdownParser.Parse("Texto com **negrito** e `código`.");

        Assert.Equal("Texto com negrito e código.", Assert.Single(blocks).Text);
    }

    [Fact]
    public void Composer_separa_capa_do_corpo()
    {
        const string manuscript = """
            # Dinheiro Sob Controle
            ## Guia do autônomo

            Introdução do livro.

            ## Capítulo 1 — Mapeie

            Corpo do capítulo.
            """;

        var book = PdfBookComposer.Build(manuscript, "fallback", "promessa", new PdfCta("Compre", null), PdfTheme.Classic);

        Assert.Equal("Dinheiro Sob Controle", book.Title);
        Assert.Equal("Guia do autônomo", book.Subtitle);
        Assert.Equal("promessa", book.Tagline);
        // a capa (H1 + subtítulo) não se repete no corpo; o capítulo permanece
        Assert.DoesNotContain(book.Body, b => b.Level == 1);
        Assert.Contains(book.Body, b => b.Kind == MarkdownBlockKind.Heading && b.Text.StartsWith("Capítulo", StringComparison.Ordinal));
    }

    [Fact]
    public void ThemeSelector_e_deterministico_por_nicho()
    {
        Assert.Equal(PdfThemeSelector.ForNiche("financas-autonomos"), PdfThemeSelector.ForNiche("financas-autonomos"));
        Assert.True(Enum.IsDefined(PdfThemeSelector.ForNiche("qualquer-nicho")));
    }
}
