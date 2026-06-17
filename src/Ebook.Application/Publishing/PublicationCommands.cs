using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;

namespace Ebook.Application.Publishing;

// ── Dados de Publicação (modal): grava nome/descrição/preço/moeda/idioma/categoria/plataforma ──

public sealed record SetPublicationDataCommand(
    Guid ProductId,
    string Platform,
    string Title,
    string Description,
    decimal Price,
    string Currency,
    string EmailLanguage,
    string Category) : ICommand<bool>;

public sealed class SetPublicationDataCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : ICommandHandler<SetPublicationDataCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(SetPublicationDataCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<bool>(PublishingErrors.ProductNotFound(command.ProductId));
        }

        if (!Enum.TryParse<PublicationPlatform>(command.Platform, ignoreCase: true, out var platform))
        {
            return Result.Failure<bool>(PublishingErrors.UnknownPlatform(command.Platform));
        }

        var result = product.SetPublicationData(
            command.Title, command.Description, command.Price, command.Currency,
            command.EmailLanguage, command.Category, platform);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ── Inserir link de checkout: persiste CheckoutUrl; a LP absorve via /go/{slug} em runtime ──

public sealed record SetCheckoutLinkCommand(Guid ProductId, string CheckoutUrl) : ICommand<bool>;

public sealed class SetCheckoutLinkCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : ICommandHandler<SetCheckoutLinkCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(SetCheckoutLinkCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<bool>(PublishingErrors.ProductNotFound(command.ProductId));
        }

        var result = product.SetCheckoutLink(command.CheckoutUrl);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ── Marcar como publicado: Publishing → Published (dispara a sincronização via ProductPublished) ──

public sealed record MarkPublishedCommand(Guid ProductId, string Platform) : ICommand<bool>;

public sealed class MarkPublishedCommandHandler(
    IProductRepository products, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<MarkPublishedCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(MarkPublishedCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<bool>(PublishingErrors.ProductNotFound(command.ProductId));
        }

        if (!Enum.TryParse<PublicationPlatform>(command.Platform, ignoreCase: true, out var platform))
        {
            return Result.Failure<bool>(PublishingErrors.UnknownPlatform(command.Platform));
        }

        var result = product.MarkPublished(platform, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ── Sincronizar (ação manual): enfileira o job de sincronização (mesma rotina do disparo automático) ──

public sealed record SyncProductCommand(Guid ProductId) : ICommand<bool>;

public sealed class SyncProductCommandHandler(IProductRepository products, IJobQueue jobQueue)
    : ICommandHandler<SyncProductCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(SyncProductCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.ProductId, ct);
        if (product is null)
        {
            return Result.Failure<bool>(PublishingErrors.ProductNotFound(command.ProductId));
        }

        if (product.Status is not (ProductStatus.Published or ProductStatus.Synchronized or ProductStatus.Unsynchronized))
        {
            return Result.Failure<bool>(PublishingErrors.NotSyncable(product.Status));
        }

        var enqueue = await jobQueue.EnqueueAsync(new JobRequest(
            PublishingJobs.Sync, command.ProductId.ToString(),
            PublishingJobs.SyncKey(command.ProductId), ProductId: command.ProductId), ct);
        return enqueue.IsFailure ? Result.Failure<bool>(enqueue.Error) : Result.Success(true);
    }
}
