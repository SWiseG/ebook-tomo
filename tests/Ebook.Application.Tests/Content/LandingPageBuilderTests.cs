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
    [InlineData(LpTemplate.Vibrant)]
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

    [Theory]
    [InlineData(LpTemplate.Aurora)]
    [InlineData(LpTemplate.Editorial)]
    [InlineData(LpTemplate.Vibrant)]
    public void Render_inclui_blocos_da_lp_2_quando_presentes(LpTemplate template)
    {
        var copy = FullCopy() with
        {
            ProofPill = "Método passo a passo · garantia de 7 dias",
            TrustBadges = ["Garantia de 7 dias", "Acesso imediato"],
            Price = new LpPriceDto(97m, 47m, Installments: 12),
            Steps = [new LpStepDto("Passo 1", "Diagnóstico", "Descubra onde está o vazamento.")],
            BonusItems = [new LpBonusDto("Checklist mensal", "Para não esquecer nada", 47m)],
            Guarantee = new LpGuaranteeDto("Garantia incondicional", "O risco é nosso.", 7),
            FinalCta = new LpFinalCtaDto("Comece hoje", "Sua virada começa agora.", "Quero agora"),
        };
        var model = LandingPageBuilder.BuildModel(
            "Dinheiro Sob Controle", copy, [1, 2, 3, 4], "/go/x", "/px.gif?s=x", Palette);

        var html = LandingPageBuilder.Render(model, template);

        // Asserções na forma class="..." (atributo no HTML) — os nomes de classe também
        // existem no CSS, então só o atributo prova que o BLOCO foi renderizado.
        Assert.Contains("class=\"proof-pill\"", html, StringComparison.Ordinal);
        Assert.Contains("Método passo a passo", html, StringComparison.Ordinal);
        Assert.Contains("class=\"trust-row\"", html, StringComparison.Ordinal);
        Assert.Contains("Garantia de 7 dias", html, StringComparison.Ordinal);
        Assert.Contains("class=\"steps\"", html, StringComparison.Ordinal); // como funciona
        Assert.Contains("Diagnóstico", html, StringComparison.Ordinal);
        Assert.Contains("class=\"bonus-stack\"", html, StringComparison.Ordinal);
        Assert.Contains("12x de", html, StringComparison.Ordinal); // parcelamento
        Assert.Contains("class=\"guarantee\"", html, StringComparison.Ordinal);
        Assert.Contains("O risco é nosso.", html, StringComparison.Ordinal);
        Assert.Contains("class=\"final-cta\"", html, StringComparison.Ordinal);
        Assert.Contains("Sua virada começa agora.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_omite_blocos_lp_2_e_prova_social_quando_ausentes()
    {
        // FullCopy() não traz campos da LP 2.0 nem dados reais de prova social.
        var model = LandingPageBuilder.BuildModel("P", FullCopy(), null, "/go/p", "/px.gif?s=p", Palette);
        var html = LandingPageBuilder.Render(model, LpTemplate.Aurora);

        Assert.Contains("class=\"proof-pill\"", html, StringComparison.Ordinal); // modo agressivo: fallback sempre
        Assert.DoesNotContain("class=\"steps\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"bonus-stack\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"installments\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"guarantee\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"final-cta\"", html, StringComparison.Ordinal);
        // Modo alta conversão: prova social no hero sempre presente (rating piso 4.9).
        Assert.Contains("class=\"hero-proof\"", html, StringComparison.Ordinal);
        // Blocos que dependem de dados ainda omitidos quando ausentes.
        Assert.DoesNotContain("class=\"testimonials\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"media-bar\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("og:image", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BonusStack_soma_valores_e_mostra_valor_cheio()
    {
        var copy = FullCopy() with
        {
            BonusItems =
            [
                new LpBonusDto("Bônus 1", "desc", 47m),
                new LpBonusDto("Bônus 2", "desc", 53m),
            ],
        };
        var model = LandingPageBuilder.BuildModel("P", copy, null, "/go/p", "/px.gif?s=p", Palette);
        var html = LandingPageBuilder.Render(model, LpTemplate.Editorial);

        Assert.Contains("bonus-total", html, StringComparison.Ordinal);
        Assert.Contains("100,00", html, StringComparison.Ordinal); // 47 + 53
    }

    [Theory]
    [InlineData(LpTemplate.Aurora)]
    [InlineData(LpTemplate.Editorial)]
    [InlineData(LpTemplate.Vibrant)]
    public void Render_tem_nav_faq_accordion_sticky_e_comportamentos(LpTemplate template)
    {
        var model = LandingPageBuilder.BuildModel(
            "Dinheiro Sob Controle", FullCopy(), null, "/go/x", "/px.gif?s=x", Palette);
        var html = LandingPageBuilder.Render(model, template);

        Assert.Contains("class=\"topnav\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#oferta\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#duvidas\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"oferta\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"duvidas\"", html, StringComparison.Ordinal);
        Assert.Contains("<details class=\"faq-item\"", html, StringComparison.Ordinal); // FAQ accordion
        Assert.Contains("<summary>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dl>", html, StringComparison.Ordinal); // não usa mais <dl>/<dt>/<dd>
        Assert.Contains("class=\"sticky-cta\"", html, StringComparison.Ordinal); // CTA fixo no mobile
        Assert.Contains("IntersectionObserver", html, StringComparison.Ordinal); // scroll-reveal
    }

    [Fact]
    public void AnnouncementBar_so_aparece_com_prazo_real_futuro()
    {
        var copy = FullCopy();

        var withDeadline = LandingPageBuilder.Render(
            LandingPageBuilder.BuildModel("P", copy, null, "/go/p", "/px.gif?s=p", Palette, DateTime.UtcNow.AddHours(48)),
            LpTemplate.Aurora);
        Assert.Contains("class=\"announce\"", withDeadline, StringComparison.Ordinal);
        Assert.Contains("data-deadline=", withDeadline, StringComparison.Ordinal);

        // Modo alta conversão: sem prazo real → contador rolante (urgência sempre presente).
        var noDeadline = LandingPageBuilder.Render(
            LandingPageBuilder.BuildModel("P", copy, null, "/go/p", "/px.gif?s=p", Palette),
            LpTemplate.Aurora);
        Assert.Contains("class=\"announce\"", noDeadline, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_inclui_seo_jsonld_fonts_e_datacta()
    {
        var model = LandingPageBuilder.BuildModel(
            "Dinheiro Sob Controle", FullCopy(), [1, 2, 3, 4], "/go/x", "/px.gif?s=x", Palette,
            offerDeadlineUtc: null,
            canonicalUrl: "https://app.tomolibrary.com.br/lp/x",
            coverImageUrl: "https://app.tomolibrary.com.br/media/products/x/images/cover.png");

        var html = LandingPageBuilder.Render(model, LpTemplate.Vibrant);

        // canonical + Open Graph + Twitter
        Assert.Contains("rel=\"canonical\"", html, StringComparison.Ordinal);
        Assert.Contains("property=\"og:title\"", html, StringComparison.Ordinal);
        Assert.Contains("property=\"og:image\"", html, StringComparison.Ordinal);
        Assert.Contains("summary_large_image", html, StringComparison.Ordinal); // tem imagem → card grande
        // webfonts do nicho com display=swap
        Assert.Contains("fonts.googleapis.com/css2", html, StringComparison.Ordinal);
        Assert.Contains("display=swap", html, StringComparison.Ordinal);
        // JSON-LD: Product + Offer + FAQPage
        Assert.Contains("application/ld+json", html, StringComparison.Ordinal);
        Assert.Contains("\"@type\":\"Product\"", html, StringComparison.Ordinal);
        Assert.Contains("\"@type\":\"FAQPage\"", html, StringComparison.Ordinal);
        Assert.Contains("\"priceCurrency\":\"BRL\"", html, StringComparison.Ordinal);
        // data-cta para analytics por posição
        Assert.Contains("data-cta=\"hero\"", html, StringComparison.Ordinal);
        Assert.Contains("data-cta=\"offer\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_inclui_showcase_e_sistema_de_icones_svg()
    {
        var copy = FullCopy() with
        {
            TrustBadges = ["Garantia de 7 dias"],
            BonusItems = [new LpBonusDto("Checklist", "desc", 47m)],
            Guarantee = new LpGuaranteeDto("Garantia", "O risco é nosso.", 7),
        };
        var model = LandingPageBuilder.BuildModel(
            "Produto X", copy, null, "/go/x", "/px.gif?s=x", Palette, showcaseImage: [9, 9, 9, 9]);

        var html = LandingPageBuilder.Render(model, LpTemplate.Vibrant);

        Assert.Contains("class=\"showcase\"", html, StringComparison.Ordinal); // ilustração IA embutida
        Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal);
        Assert.Contains("<svg class=\"ic\"", html, StringComparison.Ordinal); // sistema de ícones SVG
        Assert.Contains("class=\"g-badge\"", html, StringComparison.Ordinal); // garantia com escudo
    }

    [Fact]
    public void Render_omite_showcase_quando_sem_imagem()
    {
        var model = LandingPageBuilder.BuildModel("Produto X", FullCopy(), null, "/go/x", "/px.gif?s=x", Palette);
        var html = LandingPageBuilder.Render(model, LpTemplate.Aurora);

        Assert.DoesNotContain("class=\"showcase\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Footer_inclui_pagamento_disclaimer_e_legal_quando_presentes()
    {
        var legal = new LpLegalDto("Tomo Library Ltda", "00.000.000/0001-00", "contato@tomo.com.br",
            "https://tomo.com.br/privacidade", "https://tomo.com.br/termos");
        var model = LandingPageBuilder.BuildModel(
            "Dinheiro Sob Controle", FullCopy(), null, "/go/x", "/px.gif?s=x", Palette,
            offerDeadlineUtc: null, canonicalUrl: null, coverImageUrl: null,
            legal: legal,
            disclaimer: "Este conteúdo é educacional e não constitui recomendação de investimento.");

        var html = LandingPageBuilder.Render(model, LpTemplate.Editorial);

        Assert.Contains("Pagamento 100% seguro", html, StringComparison.Ordinal);
        Assert.Contains("Pix, cartão ou boleto", html, StringComparison.Ordinal);
        Assert.Contains("não constitui recomendação de investimento", html, StringComparison.Ordinal); // disclaimer
        Assert.Contains("Tomo Library Ltda", html, StringComparison.Ordinal); // razão social
        Assert.Contains("CNPJ 00.000.000/0001-00", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://tomo.com.br/privacidade\"", html, StringComparison.Ordinal);
        Assert.Contains("mailto:contato@tomo.com.br", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Footer_minimo_quando_sem_legal_mas_mantem_pagamento_seguro()
    {
        var model = LandingPageBuilder.BuildModel("Produto X", FullCopy(), null, "/go/x", "/px.gif?s=x", Palette);
        var html = LandingPageBuilder.Render(model, LpTemplate.Aurora);

        Assert.Contains("Pagamento 100% seguro", html, StringComparison.Ordinal); // sempre presente
        Assert.Contains("Produto X", html, StringComparison.Ordinal); // razão social = título (fallback)
        Assert.DoesNotContain("CNPJ", html, StringComparison.Ordinal); // sem CNPJ configurado
        Assert.DoesNotContain("class=\"f-links\"", html, StringComparison.Ordinal); // sem links configurados
    }

    [Fact]
    public void DisclaimerFor_varia_por_categoria_e_e_honesto()
    {
        Assert.Contains("investimento", NicheStyleCatalog.DisclaimerFor(NicheCategory.Finance), StringComparison.Ordinal);
        Assert.Contains("médica", NicheStyleCatalog.DisclaimerFor(NicheCategory.Health), StringComparison.Ordinal);
        Assert.Contains("psicológico", NicheStyleCatalog.DisclaimerFor(NicheCategory.SelfHelp), StringComparison.Ordinal);
    }

    [Fact]
    public void Selector_mapeia_template_por_categoria_e_e_deterministico()
    {
        Assert.Equal(LpTemplateSelector.ForNiche("saude"), LpTemplateSelector.ForNiche("saude"));

        // Tom × design: finanças/educação → Editorial; saúde/marketing → Vibrant; tech/ficção → Aurora.
        Assert.Equal(LpTemplate.Editorial, LpTemplateSelector.ForNiche("financas-pessoais"));
        Assert.Equal(LpTemplate.Vibrant, LpTemplateSelector.ForNiche("saude-bem-estar"));
        Assert.Equal(LpTemplate.Vibrant, LpTemplateSelector.ForNiche("marketing-digital"));
        Assert.Equal(LpTemplate.Vibrant, LpTemplateSelector.ForNiche("relacionamento-a-dois")); // relacionamento → SelfHelp
        Assert.Equal(LpTemplate.Aurora, LpTemplateSelector.ForNiche("tecnologia-ia"));

        var slugs = new[] { "financas", "saude", "marketing", "tecnologia", "ficcao-romance", "educacao" };
        var distinct = slugs.Select(LpTemplateSelector.ForNiche).Distinct().Count();
        Assert.True(distinct > 1, "o seletor deveria distribuir templates entre categorias");
    }
}
