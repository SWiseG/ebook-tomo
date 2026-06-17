using Ebook.Application.Publishing;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// Catálogo Kiwify falso para testes (sem rede). Por padrão retorna lista vazia; um teste pode
/// popular <see cref="Products"/> para simular produtos existentes na conta.
/// </summary>
public sealed class FakeKiwifyCatalog : IKiwifyCatalog
{
    public List<KiwifyCatalogProduct> Products { get; } = [];

    public Task<Result<IReadOnlyList<KiwifyCatalogProduct>>> ListProductsAsync(CancellationToken ct) =>
        Task.FromResult(Result.Success<IReadOnlyList<KiwifyCatalogProduct>>(Products));

    public Task<Result<KiwifyCatalogProduct>> GetProductAsync(string kiwifyProductId, CancellationToken ct) =>
        Task.FromResult(Products.FirstOrDefault(p => p.Id == kiwifyProductId) is { } found
            ? Result.Success(found)
            : Result.Failure<KiwifyCatalogProduct>(PublishingErrors.KiwifyApiUnavailable));
}
