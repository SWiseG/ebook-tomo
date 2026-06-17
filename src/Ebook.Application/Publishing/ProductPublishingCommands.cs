using Ebook.Application.Common.Messaging;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;

namespace Ebook.Application.Publishing;

/// <summary>Aprova um produto que aguarda aprovação (E07-03): AwaitingApproval → Publishing.</summary>
public sealed record ApproveProductCommand(Guid ProductId) : ICommand<bool>;

public sealed class ApproveProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveProductCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(ApproveProductCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<bool>(PublishingErrors.ProductNotFound(command.ProductId));
        }

        var result = product.Approve(); // → Publishing + ProductPublishingStarted (orquestra a publicação)
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>Rejeita o manuscrito/produto: volta para retrabalho (Reworking → Writing).</summary>
public sealed record RejectProductCommand(Guid ProductId, string Reason) : ICommand<bool>;

public sealed class RejectProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : ICommandHandler<RejectProductCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RejectProductCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<bool>(PublishingErrors.ProductNotFound(command.ProductId));
        }

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? "Rejeitado no painel" : command.Reason;
        var result = product.Reject(reason);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>
/// Conclui a publicação manualmente (modo manual-assistido): o operador cria o produto
/// na Kiwify, informa o id e a URL de checkout, e o produto vai a Live.
/// </summary>
public sealed record CompletePublishingCommand(Guid ProductId, string KiwifyProductId, string CheckoutUrl)
    : ICommand<bool>;

public sealed class CompletePublishingCommandHandler(
    IProductRepository products,
    IClock clock,
    IUnitOfWork unitOfWork) : ICommandHandler<CompletePublishingCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(CompletePublishingCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<bool>(PublishingErrors.ProductNotFound(command.ProductId));
        }

        // Caminho legado (detalhe): grava o checkout e marca publicado na Kiwify; a sincronização
        // (disparada por ProductPublished) confirma o produto na plataforma → Synchronized.
        var checkout = product.SetCheckoutLink(command.CheckoutUrl);
        if (checkout.IsFailure)
        {
            return Result.Failure<bool>(checkout.Error);
        }

        var result = product.MarkPublished(Domain.Products.PublicationPlatform.Kiwify, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        // O operador informou o id Kiwify manualmente (confirmação) → marca sincronizado direto.
        if (!string.IsNullOrWhiteSpace(command.KiwifyProductId))
        {
            product.MarkSynchronized(command.KiwifyProductId);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
