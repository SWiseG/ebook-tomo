using Ebook.Application.Common.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Ebook.Api.Realtime;

/// <summary>
/// Implementação SignalR do <see cref="IRealtimeNotifier"/>. Faz broadcast para
/// todos os clientes autenticados. Best-effort por contrato: engole exceções
/// (uma falha de push não pode derrubar o JobWorker nem reprocessar o Outbox).
/// </summary>
public sealed class SignalRRealtimeNotifier(
    IHubContext<TomoHub> hub,
    ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public async Task JobChangedAsync(RealtimeJobChanged change, CancellationToken ct)
    {
        try
        {
            await hub.Clients.All.SendAsync("JobChanged", change, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao emitir JobChanged para o job {JobId}", change.Id);
        }
    }

    public async Task ProductChangedAsync(RealtimeProductChanged change, CancellationToken ct)
    {
        try
        {
            await hub.Clients.All.SendAsync("ProductChanged", change, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao emitir ProductChanged para o produto {ProductId}", change.ProductId);
        }
    }
}
