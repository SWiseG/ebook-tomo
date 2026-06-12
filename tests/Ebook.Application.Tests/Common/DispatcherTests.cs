using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Application.Tests.Common;

public class DispatcherTests
{
    private sealed record PingCommand(string Message) : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<Result<string>> HandleAsync(PingCommand command, CancellationToken ct) =>
            Task.FromResult(Result.Success($"pong:{command.Message}"));
    }

    private sealed record BoomCommand : ICommand<string>;

    private sealed class BoomHandler : ICommandHandler<BoomCommand, string>
    {
        public Task<Result<string>> HandleAsync(BoomCommand command, CancellationToken ct) =>
            throw new InvalidOperationException("explodiu");
    }

    private sealed record OrphanCommand : ICommand<string>;

    private static IDispatcher BuildDispatcher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<Dispatcher>>(NullLogger<Dispatcher>.Instance);
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingHandler>();
        services.AddScoped<ICommandHandler<BoomCommand, string>, BoomHandler>();
        return services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }

    [Fact]
    public async Task SendAsync_resolve_handler_e_retorna_resultado()
    {
        var result = await BuildDispatcher().SendAsync(new PingCommand("oi"));

        Assert.True(result.IsSuccess);
        Assert.Equal("pong:oi", result.Value);
    }

    [Fact]
    public async Task SendAsync_sem_handler_registrado_retorna_falha()
    {
        var result = await BuildDispatcher().SendAsync(new OrphanCommand());

        Assert.True(result.IsFailure);
        Assert.Equal("Dispatcher.HandlerNotFound", result.Error.Code);
    }

    [Fact]
    public async Task SendAsync_excecao_no_handler_vira_result_failure()
    {
        var result = await BuildDispatcher().SendAsync(new BoomCommand());

        Assert.True(result.IsFailure);
        Assert.Equal("Dispatcher.Unhandled", result.Error.Code);
        Assert.Contains("explodiu", result.Error.Message, StringComparison.Ordinal);
    }
}
