using Ebook.Domain.Common;

namespace Ebook.Application.Common.Messaging;

#pragma warning disable CA1040 // interfaces marcadoras são o contrato do dispatcher
public interface ICommand<TResult>;

public interface IQuery<TResult>;
#pragma warning restore CA1040

public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken ct);
}

public interface IDispatcher
{
    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
