using Ebook.Application.Content.Images;

namespace Ebook.Application.Tests.Content;

public class PaletteCatalogTests
{
    [Fact]
    public void ForNiche_e_deterministico()
    {
        var a = PaletteCatalog.ForNiche("financas-autonomos");
        var b = PaletteCatalog.ForNiche("financas-autonomos");

        Assert.Equal(a, b);
        Assert.False(string.IsNullOrWhiteSpace(a.Background));
        Assert.False(string.IsNullOrWhiteSpace(a.HeadingFont));
    }

    [Fact]
    public void ForNiche_varia_entre_nichos()
    {
        var slugs = new[] { "financas", "saude", "marketing", "produtividade", "espiritualidade", "culinaria" };

        var distinct = slugs.Select(PaletteCatalog.ForNiche).Distinct().Count();

        Assert.True(distinct > 1, "o catálogo deveria distribuir paletas entre nichos");
    }
}
