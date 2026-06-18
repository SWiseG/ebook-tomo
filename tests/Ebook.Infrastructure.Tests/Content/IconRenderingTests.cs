using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Infrastructure.Content;

namespace Ebook.Infrastructure.Tests.Content;

/// <summary>Frente C/docs-12: ícones SVG vetoriais (Lucide) recoloridos por nicho nas caixas do PDF.</summary>
public class IconRenderingTests
{
    private static string? IconsDir()
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

        var icons = Path.Combine(dir.FullName, "src", "Ebook.Api", "assets", "icons");
        return Directory.Exists(icons) ? icons : null;
    }

    [Fact]
    public void Icone_recolore_currentColor_e_renderiza_caixa()
    {
        var dir = IconsDir();
        Assert.NotNull(dir); // ícones versionados em src/Ebook.Api/assets/icons

        IconRegistry.Initialize(dir!);

        var colored = IconRegistry.Colored("lightbulb", "#0E2A47");
        Assert.NotNull(colored);
        Assert.Contains("#0E2A47", colored);
        Assert.DoesNotContain("currentColor", colored);

        // PDF com caixa de destaque (que embute o ícone) renderiza válido
        var palette = NicheStyleCatalog.For(NicheCategory.Finance);
        var book = new PdfBook(
            "Título", "Sub", "Promessa", PdfTheme.Classic,
            [
                MarkdownBlock.Heading(2, "Capítulo 1"),
                MarkdownBlock.Callout("Insight rápido", "Automatize a poupança."),
                MarkdownBlock.Callout("Estudo de caso", "Resultado dobrou em 30 dias."),
            ],
            new PdfCta("Garanta", "https://x"), palette);

        var bytes = new QuestPdfRenderer().Render(book);

        Assert.True(bytes.Length > 1000);
        Assert.Equal((byte)'%', bytes[0]);
    }
}
