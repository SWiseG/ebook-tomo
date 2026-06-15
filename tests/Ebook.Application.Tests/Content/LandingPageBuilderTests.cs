using Ebook.Application.Content.Images;
using Ebook.Application.Content.Lp;

namespace Ebook.Application.Tests.Content;

public class LandingPageBuilderTests
{
    private static readonly NichePalette Palette = PaletteCatalog.ForNiche("financas-autonomos");

    private static LpCopyDto FullCopy() => new(
        Headline: "Assuma o controle",
        Subheadline: "Mesmo com renda variável",
        Bullets: ["Método de 30 dias", "Planilhas prontas"],
        PainSection: "Você nunca sabe quanto sobra.",
        SolutionSection: "Um sistema simples resolve.",
        Faq: [new LpFaqDto("Funciona para MEI?", "Sim.")],
        Price: new LpPriceDto(47m, 27m),
        Bonuses: ["Checklist mensal"]);

    [Theory]
    [InlineData(LpTemplate.Aurora)]
    [InlineData(LpTemplate.Editorial)]
    public void Render_inclui_secoes_capa_cta_e_pixel(LpTemplate template)
    {
        var model = LandingPageBuilder.BuildModel(
            "Dinheiro Sob Controle", FullCopy(), [1, 2, 3, 4],
            "/go/dinheiro-sob-controle", "/px.gif?s=dinheiro-sob-controle", Palette);

        var html = LandingPageBuilder.Render(model, template);

        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
        Assert.Contains("Assuma o controle", html, StringComparison.Ordinal); // headline
        Assert.Contains("Mesmo com renda variável", html, StringComparison.Ordinal); // subheadline
        Assert.Contains("Método de 30 dias", html, StringComparison.Ordinal); // bullet
        Assert.Contains("Você nunca sabe quanto sobra.", html, StringComparison.Ordinal); // dor
        Assert.Contains("Funciona para MEI?", html, StringComparison.Ordinal); // FAQ
        Assert.Contains("Checklist mensal", html, StringComparison.Ordinal); // bônus
        Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal); // capa embutida
        Assert.Contains("/go/dinheiro-sob-controle", html, StringComparison.Ordinal); // CTA
        Assert.Contains("/px.gif?s=dinheiro-sob-controle", html, StringComparison.Ordinal); // pixel
        Assert.Contains("URLSearchParams", html, StringComparison.Ordinal); // script de UTM (E11-01)
        Assert.Contains("utm_source", html, StringComparison.Ordinal); // repasse de UTM para pixel/CTA
        Assert.Contains("27,00", html, StringComparison.Ordinal); // preço atual (pt-BR)
        Assert.Contains("47,00", html, StringComparison.Ordinal); // âncora riscada
        Assert.EndsWith("</html>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_escapa_html_da_copy()
    {
        var copy = FullCopy() with { Headline = "Preço <b>imbatível</b> & \"oferta\"" };
        var model = LandingPageBuilder.BuildModel("P", copy, null, "/go/p", "/px.gif?s=p", Palette);

        var html = LandingPageBuilder.Render(model, LpTemplate.Aurora);

        Assert.DoesNotContain("<b>imbatível</b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;", html, StringComparison.Ordinal); // tags escapadas
    }

    [Fact]
    public void BuildModel_usa_titulo_quando_copy_ausente_e_omite_preco_zero()
    {
        var model = LandingPageBuilder.BuildModel(
            "Título do Produto", copy: null, coverImage: null,
            "/go/x", "/px.gif?s=x", Palette);

        Assert.Equal("Título do Produto", model.Headline); // fallback
        Assert.Null(model.CoverDataUri);
        Assert.Empty(model.Bullets);

        var html = LandingPageBuilder.Render(model, LpTemplate.Aurora);
        Assert.Contains("Título do Produto", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"price\"", html, StringComparison.Ordinal); // sem preço → sem bloco
        Assert.DoesNotContain("data:image", html, StringComparison.Ordinal); // sem capa
    }

    [Fact]
    public void Selector_e_deterministico_e_distribui_entre_nichos()
    {
        Assert.Equal(LpTemplateSelector.ForNiche("saude"), LpTemplateSelector.ForNiche("saude"));

        var slugs = new[] { "financas", "saude", "marketing", "produtividade", "espiritualidade", "culinaria" };
        var distinct = slugs.Select(LpTemplateSelector.ForNiche).Distinct().Count();
        Assert.True(distinct > 1, "o seletor deveria distribuir templates entre nichos");
    }
}
