namespace Ebook.Application.Common.Realtime;

/// <summary>
/// Implementação padrão no-op de <see cref="IRealtimeNotifier"/>, registrada por
/// <c>AddApplication</c>. Garante que handlers que dependem da interface (ex.:
/// <see cref="ProductRealtimeHandler"/>) sejam sempre construíveis — inclusive em
/// testes, que não sobem o SignalR. Em produção a Api registra a implementação
/// SignalR depois desta, e a última registrada vence na injeção.
/// </summary>
public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task JobChangedAsync(RealtimeJobChanged change, CancellationToken ct) => Task.CompletedTask;

    public Task ProductChangedAsync(RealtimeProductChanged change, CancellationToken ct) => Task.CompletedTask;
}
