using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Infrastructure.Content;

namespace Ebook.Infrastructure.Tests.Content;

/// <summary>Frente A (docs/11): fontes profissionais embarcadas registram e renderizam um PDF válido.</summary>
public class FontEmbeddingTests
{
    private static string? FontsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ebook.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            return null;
        }

        var fonts = Path.Combine(dir.FullName, "src", "Ebook.Api", "assets", "fonts");
        return Directory.Exists(fonts) ? fonts : null;
    }

    [Fact]
    public void Fontes_embarcadas_registram_no_skia_e_renderizam_pdf()
    {
        var dir = FontsDir();
        Assert.NotNull(dir); // versionadas em src/Ebook.Api/assets/fonts

        FontRegistry.Initialize(dir!);

        // família derivada do nome do arquivo (independe dos nomes internos do .ttf)
        Assert.NotNull(FontRegistry.Resolve("Merriweather"));
        Assert.NotNull(FontRegistry.Resolve("Merriweather", bold: true));
        Assert.NotNull(FontRegistry.Resolve("Playfair Display"));

        // fontes display da capa (docs/14 WP-3) — single-weight resolve mesmo pedindo bold (fallback)
        Assert.NotNull(FontRegistry.Resolve("Anton"));
        Assert.NotNull(FontRegistry.Resolve("Archivo Black", bold: true));
        Assert.NotNull(FontRegistry.Resolve("Bebas Neue"));
        Assert.NotNull(FontRegistry.Resolve("Fjalla One"));
        Assert.NotNull(FontRegistry.Resolve("Barlow Condensed", bold: true));

        // PDF com paleta de nicho (fontes embarcadas) renderiza um documento válido
        var palette = NicheStyleCatalog.For(NicheCategory.Finance);
        var book = new PdfBook(
            "Título do Livro", "Subtítulo", "Promessa", PdfTheme.Classic,
            [MarkdownBlock.Heading(2, "Capítulo 1"), MarkdownBlock.Paragraph("Corpo do capítulo de teste.")],
            new PdfCta("Garanta agora", "https://exemplo"), palette);

        var bytes = new QuestPdfRenderer().Render(book);

        Assert.True(bytes.Length > 1000);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }
}
