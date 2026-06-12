using Ebook.Domain.Common;

namespace Ebook.Application.Common.Jobs;

public sealed record JobRequest(
    string Type,
    string PayloadJson,
    string IdempotencyKey,
    Guid? ProductId = null,
    DateTime? ScheduledAtUtc = null);

/// <summary>
/// Fila persistida (SQLite). Enfileirar com IdempotencyKey já existente é no-op.
/// </summary>
public interface IJobQueue
{
    Task<Result> EnqueueAsync(JobRequest request, CancellationToken ct = default);
}

/// <summary>
/// Executor de um tipo de job. Implementações registradas no DI; o worker
/// resolve pelo <see cref="Type"/>. Deve ser idempotente (entrega at-least-once).
/// </summary>
public interface IJobHandler
{
    string Type { get; }
    Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct);
}
