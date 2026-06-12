using Ebook.Domain.Products;

namespace Ebook.Domain.Tests.Products;

public class ProductTests
{
    private static readonly DateTime Now = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    private static Product NewProduct() =>
        Product.Create(Guid.NewGuid(), "emagrecimento-pos-40", "Emagrecimento Pós-40", QualityTier.Commercial, Now);

    private static Product ProductAtLpStage()
    {
        var product = NewProduct();
        for (var i = 0; i < 4; i++) // Outline → Writing → Review → Pdf → Lp
        {
            Assert.True(product.AdvanceStage().IsSuccess);
        }

        return product;
    }

    [Fact]
    public void Create_inicia_em_pipeline_outline_e_emite_evento()
    {
        var product = NewProduct();

        Assert.Equal(ProductStatus.Pipeline, product.Status);
        Assert.Equal(ProductStage.Outline, product.Stage);
        Assert.Single(product.DomainEvents.OfType<ProductCreated>());
    }

    [Fact]
    public void AdvanceStage_percorre_pipeline_na_ordem()
    {
        var product = NewProduct();
        var stages = new List<ProductStage> { product.Stage };

        while (product.AdvanceStage().IsSuccess)
        {
            stages.Add(product.Stage);
        }

        Assert.Equal(
            [ProductStage.Outline, ProductStage.Writing, ProductStage.Review, ProductStage.Pdf, ProductStage.Lp],
            stages);
    }

    [Fact]
    public void AdvanceStage_alem_de_lp_falha()
    {
        var product = ProductAtLpStage();

        var result = product.AdvanceStage();

        Assert.True(result.IsFailure);
        Assert.Equal("Product.CannotAdvance", result.Error.Code);
    }

    [Fact]
    public void Fluxo_aprovacao_publicacao_completo()
    {
        var product = ProductAtLpStage();

        Assert.True(product.SubmitForApproval().IsSuccess);
        Assert.Equal(ProductStatus.AwaitingApproval, product.Status);

        Assert.True(product.Approve().IsSuccess);
        Assert.Equal(ProductStatus.Publishing, product.Status);

        Assert.True(product.MarkPublished("kw-123", "https://kiwify/checkout", "https://lp/slug", Now).IsSuccess);
        Assert.Equal(ProductStatus.Live, product.Status);
        Assert.Equal(ProductStage.Live, product.Stage);
        Assert.Equal("kw-123", product.KiwifyProductId);
        Assert.NotNull(product.PublishedAtUtc);
        Assert.Single(product.DomainEvents.OfType<ProductPublished>());
    }

    [Fact]
    public void Reject_volta_para_reworking_em_writing()
    {
        var product = ProductAtLpStage();
        product.SubmitForApproval();

        var result = product.Reject("capítulo 3 raso");

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Reworking, product.Status);
        Assert.Equal(ProductStage.Writing, product.Stage);
    }

    [Fact]
    public void BeginPublishing_modo_auto_pula_aprovacao()
    {
        var product = ProductAtLpStage();

        Assert.True(product.BeginPublishing().IsSuccess);
        Assert.Equal(ProductStatus.Publishing, product.Status);
    }

    [Fact]
    public void SubmitForApproval_fora_do_estagio_lp_falha()
    {
        var product = NewProduct(); // ainda em Outline

        Assert.True(product.SubmitForApproval().IsFailure);
    }

    [Fact]
    public void MarkPublished_sem_estar_publishing_falha()
    {
        var product = ProductAtLpStage();

        Assert.True(product.MarkPublished("kw", "url", "lp", Now).IsFailure);
    }

    [Fact]
    public void Retire_de_live_funciona_e_segunda_vez_falha()
    {
        var product = ProductAtLpStage();
        product.BeginPublishing();
        product.MarkPublished("kw", "url", "lp", Now);

        Assert.True(product.Retire("ROI negativo após 2 ciclos", Now).IsSuccess);
        Assert.Equal(ProductStatus.Retired, product.Status);
        Assert.True(product.Retire("de novo", Now).IsFailure);
    }

    [Fact]
    public void Iteracao_live_para_iterating_e_volta()
    {
        var product = ProductAtLpStage();
        product.BeginPublishing();
        product.MarkPublished("kw", "url", "lp", Now);

        Assert.True(product.StartIteration().IsSuccess);
        Assert.Equal(ProductStatus.Iterating, product.Status);
        Assert.True(product.CompleteIteration().IsSuccess);
        Assert.Equal(ProductStatus.Live, product.Status);
    }
}
