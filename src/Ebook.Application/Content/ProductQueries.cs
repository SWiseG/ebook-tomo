using Ebook.Application.Common.Messaging;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;

namespace Ebook.Application.Content;

public sealed record ProductListItemDto(
    Guid Id,
    string Slug,
    string Title,
    string Status,
    string Stage,
    decimal Price,
    string Currency,
    DateTime CreatedAtUtc);

public sealed record ProductDetailDto(
    Guid Id,
    Guid NicheId,
    string Slug,
    string Title,
    string Status,
    string Stage,
    string QualityTier,
    decimal Price,
    string Currency,
    string? LpUrl,
    string? CheckoutUrl,
    string? KiwifyProductId,
    string SalesCopyJson,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc);

/// <summary>Leitura projetada de produtos para o painel (consulta direta na Infrastructure).</summary>
public interface IProductReader
{
    Task<IReadOnlyList<ProductListItemDto>> ListAsync(string? status, CancellationToken ct);
    Task<ProductDetailDto?> GetDetailAsync(Guid id, CancellationToken ct);
}

public sealed record GetProductsQuery(string? Status = null) : IQuery<IReadOnlyList<ProductListItemDto>>;

public sealed class GetProductsQueryHandler(IProductReader reader)
    : IQueryHandler<GetProductsQuery, IReadOnlyList<ProductListItemDto>>
{
    public async Task<Result<IReadOnlyList<ProductListItemDto>>> HandleAsync(GetProductsQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(query.Status, ct));
}

public sealed record GetProductDetailQuery(Guid Id) : IQuery<ProductDetailDto>;

public sealed class GetProductDetailQueryHandler(IProductReader reader)
    : IQueryHandler<GetProductDetailQuery, ProductDetailDto>
{
    public async Task<Result<ProductDetailDto>> HandleAsync(GetProductDetailQuery query, CancellationToken ct)
    {
        var detail = await reader.GetDetailAsync(query.Id, ct);
        return detail is null
            ? Result.Failure<ProductDetailDto>(ContentErrors.ProductNotFound(query.Id))
            : Result.Success(detail);
    }
}

public sealed record GetManuscriptQuery(Guid ProductId) : IQuery<string>;

public sealed class GetManuscriptQueryHandler(IProductRepository products, IFileStore fileStore)
    : IQueryHandler<GetManuscriptQuery, string>
{
    public async Task<Result<string>> HandleAsync(GetManuscriptQuery query, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(query.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<string>(ContentErrors.ProductNotFound(query.ProductId));
        }

        var content = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1), ct);
        return content is null
            ? Result.Failure<string>(ContentErrors.ManuscriptMissing(product.Slug))
            : Result.Success(content);
    }
}

public sealed record GetProductPdfQuery(Guid ProductId) : IQuery<byte[]>;

public sealed class GetProductPdfQueryHandler(IProductRepository products, IArtifactStore artifactStore)
    : IQueryHandler<GetProductPdfQuery, byte[]>
{
    public async Task<Result<byte[]>> HandleAsync(GetProductPdfQuery query, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(query.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<byte[]>(ContentErrors.ProductNotFound(query.ProductId));
        }

        var bytes = await artifactStore.ReadBytesAsync(ContentPaths.Pdf(product.Slug, 1), ct);
        return bytes is null
            ? Result.Failure<byte[]>(ContentErrors.PdfMissing(product.Slug))
            : Result.Success(bytes);
    }
}

public sealed record GetProductCoverQuery(Guid ProductId) : IQuery<byte[]>;

public sealed class GetProductCoverQueryHandler(IProductRepository products, IArtifactStore artifactStore)
    : IQueryHandler<GetProductCoverQuery, byte[]>
{
    public async Task<Result<byte[]>> HandleAsync(GetProductCoverQuery query, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(query.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<byte[]>(ContentErrors.ProductNotFound(query.ProductId));
        }

        var bytes = await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug), ct);
        return bytes is null
            ? Result.Failure<byte[]>(ContentErrors.CoverMissing(product.Slug))
            : Result.Success(bytes);
    }
}

public sealed record GetOutlineQuery(Guid ProductId) : IQuery<OutlineDto>;

public sealed class GetOutlineQueryHandler(IProductRepository products, IFileStore fileStore)
    : IQueryHandler<GetOutlineQuery, OutlineDto>
{
    public async Task<Result<OutlineDto>> HandleAsync(GetOutlineQuery query, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(query.ProductId, ct);
        return product is null
            ? Result.Failure<OutlineDto>(ContentErrors.ProductNotFound(query.ProductId))
            : await ContentPaths.ReadOutlineAsync(fileStore, product.Slug, ct);
    }
}
