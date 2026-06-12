using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Common.Messaging;

/// <summary>
/// CQRS-lite: resolve o handler no container e invoca com logging estruturado.
/// Sem MediatR — pipeline explícito e sem dependência licenciada.
/// </summary>
public sealed class Dispatcher(IServiceProvider serviceProvider, ILogger<Dispatcher> logger) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethodCache = new();

    public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        DispatchAsync<TResult>(typeof(ICommandHandler<,>), command, ct);

    public Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        DispatchAsync<TResult>(typeof(IQueryHandler<,>), query, ct);

    private async Task<Result<TResult>> DispatchAsync<TResult>(Type handlerOpenType, object message, CancellationToken ct)
    {
        var messageType = message.GetType();
        var handlerType = handlerOpenType.MakeGenericType(messageType, typeof(TResult));
        var handler = serviceProvider.GetService(handlerType);

        if (handler is null)
        {
            logger.LogError("Nenhum handler registrado para {MessageType}", messageType.Name);
            return Result.Failure<TResult>(new Error("Dispatcher.HandlerNotFound",
                $"Nenhum handler registrado para {messageType.Name}."));
        }

        var method = HandleMethodCache.GetOrAdd(handlerType, t => t.GetMethod("HandleAsync")!);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var task = (Task<Result<TResult>>)method.Invoke(handler, [message, ct])!;
            var result = await task;
            logger.LogInformation("{MessageType} processado em {ElapsedMs}ms — sucesso: {IsSuccess}",
                messageType.Name, stopwatch.ElapsedMilliseconds, result.IsSuccess);
            return result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            logger.LogError(ex.InnerException, "{MessageType} falhou em {ElapsedMs}ms",
                messageType.Name, stopwatch.ElapsedMilliseconds);
            return Result.Failure<TResult>(new Error("Dispatcher.Unhandled", ex.InnerException.Message));
        }
    }
}
