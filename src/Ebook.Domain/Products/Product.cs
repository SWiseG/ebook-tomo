using Ebook.Domain.Common;

namespace Ebook.Domain.Products;

public enum ProductStatus
{
    Pipeline,
    AwaitingApproval,
    Reworking,
    Publishing,
    Published,
    Synchronized,
    Unsynchronized,
    Live, // legado (substituído por Synchronized no fluxo de publicação)
    Iterating,
    Retired
}

/// <summary>Plataforma onde o produto foi publicado manualmente (a criação não é via API).</summary>
public enum PublicationPlatform
{
    Kiwify,
    Hotmart
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

    // Dados de publicação (preenchidos no painel para a criação manual na plataforma).
    public string? Description { get; private set; }
    public string? EmailLanguage { get; private set; }
    public string? Category { get; private set; }
    public PublicationPlatform? PublicationPlatform { get; private set; }
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

    /// <summary>Dados de publicação (modal "Dados de Publicação"); só em Publishing. Descrição ≥ 100 chars.</summary>
    public Result SetPublicationData(
        string title, string description, decimal price, string currency,
        string emailLanguage, string category, PublicationPlatform platform)
    {
        if (Status != ProductStatus.Publishing || Stage != ProductStage.Publishing)
        {
            return Result.Failure(ProductErrors.NotInPublishing(Status));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 100)
        {
            return Result.Failure(ProductErrors.DescriptionTooShort);
        }

        Title = title;
        Description = description;
        Price = price;
        Currency = currency;
        EmailLanguage = emailLanguage;
        Category = category;
        PublicationPlatform = platform;
        return Result.Success();
    }

    /// <summary>Insere/atualiza o link de checkout; a LP o absorve em runtime via /go/{slug}.</summary>
    public Result SetCheckoutLink(string checkoutUrl)
    {
        if (Status is not (ProductStatus.Publishing or ProductStatus.Published
            or ProductStatus.Synchronized or ProductStatus.Unsynchronized))
        {
            return Result.Failure(ProductErrors.NotPublishable(Status));
        }

        CheckoutUrl = checkoutUrl;
        return Result.Success();
    }

    /// <summary>Marca como publicado na plataforma escolhida (Publishing → Published) e dispara a sincronização.</summary>
    public Result MarkPublished(PublicationPlatform platform, DateTime utcNow)
    {
        if (Status != ProductStatus.Publishing)
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Published));
        }

        PublicationPlatform = platform;
        Status = ProductStatus.Published;
        Stage = ProductStage.Publishing;
        PublishedAtUtc = utcNow;
        Raise(new ProductPublished(Id, platform.ToString()));
        return Result.Success();
    }

    /// <summary>Sincronização confirmou o produto na plataforma (→ Synchronized, o estado vendendo).</summary>
    public Result MarkSynchronized(string kiwifyProductId)
    {
        if (Status is not (ProductStatus.Published or ProductStatus.Synchronized or ProductStatus.Unsynchronized))
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Synchronized));
        }

        KiwifyProductId = kiwifyProductId;
        Status = ProductStatus.Synchronized;
        Stage = ProductStage.Live;
        Raise(new ProductSynchronized(Id));
        return Result.Success();
    }

    /// <summary>Sincronização não encontrou o produto na plataforma (→ Unsynchronized; requer atenção).</summary>
    public Result MarkUnsynchronized()
    {
        if (Status is not (ProductStatus.Published or ProductStatus.Synchronized or ProductStatus.Unsynchronized))
        {
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Unsynchronized));
        }

        Status = ProductStatus.Unsynchronized;
        Raise(new ProductUnsynchronized(Id));
        return Result.Success();
    }

    public Result StartIteration()
    {
        if (Status != ProductStatus.Synchronized)
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
            return Result.Failure(ProductErrors.InvalidTransition(Status, ProductStatus.Synchronized));
        }

        Status = ProductStatus.Synchronized;
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

    /// <summary>Registra a URL pública da landing page gerada (etapa Lp).</summary>
    public void SetLpUrl(string lpUrl) => LpUrl = lpUrl;

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

    public static Error NotInPublishing(ProductStatus status) =>
        new("Product.NotInPublishing", $"Produto com status {status} não está em publicação.");

    public static Error NotPublishable(ProductStatus status) =>
        new("Product.NotPublishable", $"Produto com status {status} não aceita link de checkout.");

    public static Error DescriptionTooShort =>
        new("Product.DescriptionTooShort", "A descrição deve ter pelo menos 100 caracteres.");
}

public sealed record ProductCreated(Guid ProductId, Guid NicheId, string Slug) : DomainEvent;
public sealed record ProductStageAdvanced(Guid ProductId, ProductStage Stage) : DomainEvent;
public sealed record ProductSubmittedForApproval(Guid ProductId) : DomainEvent;
public sealed record ProductRejected(Guid ProductId, string Reason) : DomainEvent;
public sealed record ProductPublishingStarted(Guid ProductId) : DomainEvent;
public sealed record ProductPublished(Guid ProductId, string Platform) : DomainEvent;
public sealed record ProductSynchronized(Guid ProductId) : DomainEvent;
public sealed record ProductUnsynchronized(Guid ProductId) : DomainEvent;
public sealed record ProductRetired(Guid ProductId, string Reason) : DomainEvent;
