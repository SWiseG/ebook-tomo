namespace Ebook.Application.Common.Realtime;

/// <summary>
/// Abstração de notificações em tempo real (push para o painel). A implementação
/// concreta (SignalR) vive na camada Api; Application/Domain só conhecem esta
/// interface, preservando a regra de dependência.
///
/// Contrato de robustez: implementações DEVEM ser best-effort — nunca lançar.
/// Uma notificação perdida é irrelevante (o cliente refaz o fetch ao reconectar),
/// mas uma exceção aqui bloquearia o JobWorker ou faria o Outbox reprocessar.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Emite que um job mudou de estado (Pending/Running/Succeeded/Dead).</summary>
    Task JobChangedAsync(RealtimeJobChanged change, CancellationToken ct);

    /// <summary>Emite que um produto sofreu uma transição relevante (o cliente refaz o fetch).</summary>
    Task ProductChangedAsync(RealtimeProductChanged change, CancellationToken ct);
}

/// <summary>Snapshot mínimo de um job para atualização incremental no painel.</summary>
public sealed record RealtimeJobChanged(
    Guid Id,
    string Type,
    string Status,
    int Attempts,
    Guid? ProductId,
    string? LastError);

/// <summary>
/// Sinal de que algo mudou para um produto. Carrega o nome do evento de domínio
/// (ex.: "ProductStageAdvanced") para o painel exibir feedback; o estado completo
/// é re-buscado pela API.
/// </summary>
public sealed record RealtimeProductChanged(Guid ProductId, string Event);
