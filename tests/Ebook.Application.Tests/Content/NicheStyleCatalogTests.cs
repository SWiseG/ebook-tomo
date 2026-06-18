using Ebook.Application.Content.Images;

namespace Ebook.Application.Tests.Content;

public class NicheStyleCatalogTests
{
    [Theory]
    [InlineData("financas-pessoais", NicheCategory.Finance)]
    [InlineData("Investimentos e Renda Passiva", NicheCategory.Finance)]
    [InlineData("emagrecimento-definitivo", NicheCategory.Health)]
    [InlineData("Saúde e Bem-estar", NicheCategory.Health)]
    [InlineData("marketing-digital", NicheCategory.Marketing)]
    [InlineData("produtividade-e-habitos", NicheCategory.SelfHelp)]
    [InlineData("inteligencia artificial para iniciantes", NicheCategory.Tech)]
    [InlineData("Romance de época", NicheCategory.Fiction)]
    [InlineData("Inglês para concursos", NicheCategory.Education)]
    [InlineData("colecionismo-de-selos", NicheCategory.General)]
    public void Classify_mapeia_por_palavra_chave(string niche, NicheCategory expected)
    {
        Assert.Equal(expected, NicheStyleCatalog.Classify(niche));
    }

    [Fact]
    public void Classify_ignora_acentos_e_caixa()
    {
        Assert.Equal(NicheStyleCatalog.Classify("FINANÇAS"), NicheStyleCatalog.Classify("financas"));
    }

    [Fact]
    public void Paletas_nunca_usam_fontes_de_sistema_proibidas()
    {
        // docs/11: nunca Times New Roman/Arial. Toda categoria usa fonte profissional embarcada.
        var banned = new[] { "Arial", "Times New Roman", "Comic Sans MS", "Papyrus" };
        var allowed = new[] { "Inter", "Manrope", "Merriweather", "Lora", "Fraunces", "Playfair Display" };

        foreach (var category in Enum.GetValues<NicheCategory>())
        {
            var p = NicheStyleCatalog.For(category);
            Assert.DoesNotContain(p.HeadingFont, banned);
            Assert.DoesNotContain(p.BodyFont, banned);
            Assert.Contains(p.HeadingFont, allowed);
            Assert.Contains(p.BodyFont, allowed);
            Assert.StartsWith("#", p.Background);
            Assert.StartsWith("#", p.Accent);
        }
    }

    [Fact]
    public void Nichos_diferentes_tem_identidades_distintas()
    {
        var finance = NicheStyleCatalog.For(NicheCategory.Finance);
        var health = NicheStyleCatalog.For(NicheCategory.Health);
        var fiction = NicheStyleCatalog.For(NicheCategory.Fiction);

        Assert.NotEqual(finance.Background, health.Background);
        Assert.NotEqual(finance.HeadingFont + finance.Background, fiction.HeadingFont + fiction.Background);
    }
}
