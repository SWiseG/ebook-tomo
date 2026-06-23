using Ebook.Application.Content.Images;
using Ebook.Infrastructure.Content;

namespace Ebook.Infrastructure.Tests.Content;

public class SkiaImageComposerTests
{
    private static readonly NichePalette Palette = PaletteCatalog.ForNiche("financas-autonomos");

    private static bool IsPng(byte[] bytes) =>
        bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G';

    [Fact]
    public void RenderCover_produz_png_valido()
    {
        var composer = new SkiaImageComposer();

        var png = composer.RenderCover(new CoverArt("Dinheiro Sob Controle", "O guia do autônomo", "Marca", Palette));

        Assert.True(IsPng(png));
        Assert.True(png.Length > 1000);
    }

    [Fact]
    public void RenderCover_rica_com_eyebrow_features_e_selo_produz_png_valido()
    {
        var composer = new SkiaImageComposer();
        var art = new CoverArt(
            "Marketing Digital do Zero ao Resultado Estratégico", "O guia completo", "Marca", Palette,
            Eyebrow: "Guia Completo",
            Features:
            [
                new CoverFeature("Atraia, nutra e converta com método"),
                new CoverFeature("SEO e tráfego pago que vendem"),
                new CoverFeature("Funil completo passo a passo"),
            ],
            Seal: "Método Validado",
            Author: "Por Especialista");

        var png = composer.RenderCover(art);

        Assert.True(IsPng(png));
        Assert.True(png.Length > 1000);
    }

    [Fact]
    public void RenderMockup_a_partir_da_capa_produz_png_valido()
    {
        var composer = new SkiaImageComposer();
        var cover = composer.RenderCover(new CoverArt("Título", null, null, Palette));

        var mockup = composer.RenderMockup(cover, Palette);

        Assert.True(IsPng(mockup));
        Assert.True(mockup.Length > 1000);
    }

    [Theory]
    [InlineData(ImageTemplate.SocialCard)]
    [InlineData(ImageTemplate.Story)]
    public void RenderSocial_produz_png_valido(ImageTemplate template)
    {
        var composer = new SkiaImageComposer();

        var png = composer.RenderSocial(new SocialArt("Headline forte", "subtexto", template, Palette));

        Assert.True(IsPng(png));
        Assert.True(png.Length > 1000);
    }
}
