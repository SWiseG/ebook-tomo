using Ebook.Domain.Common;

namespace Ebook.Domain.Products;

public enum ProductStatus
{
    Pipeline,
    AwaitingApproval,
    Reworking,
    Publishing,
    Live,
    Iterating,
    Retired
}

public enum ProductStage
{
    Outline,
    Writing,
    Review,
    Pdf,
    Lp,
    Publishing,
    Live
}

public enum QualityTier
{
    Draft,
    Commercial,
    Premium
}

public sealed class Product : AggregateRoot
{
    private static readonly ProductStage[] PipelineOrder =
        [ProductStage.Outline, ProductStage.Writing, ProductStage.Review, ProductStage.Pdf, ProductStage.Lp];

    private Product()
    {
        Slug = string.Empty;
        Title = string.Empty;
        Currency = "BRL";
        SalesCopyJson = "{}";
    }

    public Guid NicheId { get; private set; }
    public string Slug { get; private set; }
    public string Title { get; private set; }
    public ProductStatus Status { get; private set; }
    public ProductStage Stage { get; private set; }
    public QualityTier QualityTier { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; }
    public string? KiwifyProductId { get; private set; }
    public string? CheckoutUrl { get; private set; }
    public string? LpUrl { get; private set; }
    public string SalesCopyJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? RetiredAtUtc { get; private set; }

    public static Product Create(Guid nicheId, string slug, string title, QualityTier tier, DateTime utcNow)
    {
        var product = new Product
        {
            NicheId = nicheId,
            Slug = slug,
            Title = title,
            Status = ProductStatus.Pipeline,
            Stage = ProductStage.Outline,
            QualityTier = tier,
            CreatedAtUtc = utcNow
        };
        product.Raise(new ProductCreated(product.Id, nicheId, slug));
        return product;
    }

    public Result AdvanceStage()
    {
        if (Status is not (ProductStatus.Pipeline or ProductStatus.Reworking))
        {
            return Result.Failure(ProductErrors.NotInPipeline(Status));
        }

        var index = Array.IndexOf(PipelineOrder, Stage);
        if (index < 0 || index == PipelineOrder.Length - 1)
        {
            return Result.Failure(ProductErrors.CannotAdvanceFrom(Stage));
        }

        Stage = PipelineOrder[index + 1];
        Status = ProductStatus.Pipeline;
        Raise(new ProductStageAdvanced(Id, Stage));
        return Result.Success();
    }

    public Result SubmitForApproval()
    {
        if (Status is not (ProductStatus.Pipeline or ProductStatus.Reworking) || Stage != ProductStage.Lp)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.AwaitingApproval));
        }

        Status = ProductStatus.AwaitingApproval;
        Raise(new ProductSubmittedForApproval(Id));
        return Result.Success();
    }

    public Result Approve()
    {
        if (Status != ProductStatus.AwaitingApproval)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Publishing));
        }

        return BeginPublishingCore();
    }

    public Result Reject(string reason)
    {
        if (Status != ProductStatus.AwaitingApproval)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Reworking));
        }

        Status = ProductStatus.Reworking;
        Stage = ProductStage.Writing;
        Raise(new ProductRejected(Id, reason));
        return Result.Success();
    }

    /// <summary>Modo Auto: publica direto do fim do pipeline, sem gate de aprovação.</summary>
    public Result BeginPublishing()
    {
        if (Status != ProductStatus.Pipeline || Stage != ProductStage.Lp)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Publishing));
        }

        return BeginPublishingCore();
    }

    private Result BeginPublishingCore()
    {
        Status = ProductStatus.Publishing;
        Stage = ProductStage.Publishing;
        Raise(new ProductPublishingStarted(Id));
        return Result.Success();
    }

    public Result MarkPublished(string kiwifyProductId, string checkoutUrl, string lpUrl, DateTime utcNow)
    {
        if (Status != ProductStatus.Publishing)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Live));
        }

        KiwifyProductId = kiwifyProductId;
        CheckoutUrl = checkoutUrl;
        LpUrl = lpUrl;
        Status = ProductStatus.Live;
        Stage = ProductStage.Live;
        PublishedAtUtc = utcNow;
        Raise(new ProductPublished(Id, kiwifyProductId, checkoutUrl));
        return Result.Success();
    }

    public Result StartIteration()
    {
        if (Status != ProductStatus.Live)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Iterating));
        }

        Status = ProductStatus.Iterating;
        return Result.Success();
    }

    public Result CompleteIteration()
    {
        if (Status != ProductStatus.Iterating)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Live));
        }

        Status = ProductStatus.Live;
        return Result.Success();
    }

    public Result Retire(string reason, DateTime utcNow)
    {
        if (Status == ProductStatus.Retired)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Retired));
        }

        Status = ProductStatus.Retired;
        RetiredAtUtc = utcNow;
        Raise(new ProductRetired(Id, reason));
        return Result.Success();
    }

    public void SetPricing(decimal price, string currency)
    {
        Price = price;
        Currency = currency;
    }

    public void SetSalesCopy(string salesCopyJson) => SalesCopyJson = salesCopyJson;

    /// <summary>Refina o título a partir do outline gerado (o título inicial é provisório).</summary>
    public void SetTitle(string title) => Title = title;
}

public static class ProductErrors
{
    public static Error NotInPipeline(ProductStatus status) =>
        new("Product.NotInPipeline", $"Produto com status {status} não está em pipeline de geração.");

    public static Error CannotAdvanceFrom(ProductStage stage) =>
        new("Product.CannotAdvance", $"Não é possível avançar a partir do estágio {stage}.");

    public static Error InvalidTransition(ProductStatus from, ProductStatus to) =>
        new("Product.InvalidTransition", $"Transição inválida de {from} para {to}.");
}

public sealed record ProductCreated(Guid ProductId, Guid NicheId, string Slug) : DomainEvent;
public sealed record ProductStageAdvanced(Guid ProductId, ProductStage Stage) : DomainEvent;
public sealed record ProductSubmittedForApproval(Guid ProductId) : DomainEvent;
public sealed record ProductRejected(Guid ProductId, string Reason) : DomainEvent;
public sealed record ProductPublishingStarted(Guid ProductId) : DomainEvent;
public sealed record ProductPublished(Guid ProductId, string KiwifyProductId, string CheckoutUrl) : DomainEvent;
public sealed record ProductRetired(Guid ProductId, string Reason) : DomainEvent;
