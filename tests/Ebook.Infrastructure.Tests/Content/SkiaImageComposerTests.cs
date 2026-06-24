using Ebook.Application.Content.Images;
using Ebook.Infrastructure.Content;
using SkiaSharp;

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

    private static (int W, int H) PngSize(byte[] png) =>
        ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19],
         (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);

    [Theory]
    [InlineData(512, 1024)]  // retrato
    [InlineData(800, 800)]   // quadrado
    [InlineData(2000, 300)]  // panorama
    public void FitBanner_normaliza_qualquer_proporcao_para_1280x640(int w, int h)
    {
        using var s = SKSurface.Create(new SKImageInfo(w, h));
        s.Canvas.Clear(SKColors.CornflowerBlue);
        using var img = s.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);

        var banner = new SkiaImageComposer().FitBanner(data.ToArray());
        Assert.True(IsPng(banner));
        Assert.Equal((1280, 640), PngSize(banner));
    }

    [Fact]
    public void RenderMarketplaceBanner_produz_1200x1000()
    {
        var composer = new SkiaImageComposer();
        var cover = composer.RenderCover(new CoverArt("Título", "sub", null, Palette));

        var banner = composer.RenderMarketplaceBanner(cover, Palette);

        Assert.True(IsPng(banner));
        Assert.Equal((1200, 1000), PngSize(banner));
    }

    [Fact]
    public void RenderCover_produz_1600x2400_2x3()
    {
        var png = new SkiaImageComposer().RenderCover(new CoverArt("T", "s", null, Palette));
        Assert.Equal((1600, 2400), PngSize(png));
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
