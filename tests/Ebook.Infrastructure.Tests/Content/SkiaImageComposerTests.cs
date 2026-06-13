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
