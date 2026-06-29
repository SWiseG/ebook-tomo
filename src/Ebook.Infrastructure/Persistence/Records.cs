namespace Ebook.Infrastructure.Persistence;

/// <summary>Domain Event serializado na mesma transação do agregado (padrão Outbox).</summary>
public sealed class OutboxEventRecord
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

/// <summary>Idempotência de handlers: (evento, handler) processado é no-op na reentrega.</summary>
public sealed class ProcessedEventRecord
{
    public Guid EventId { get; set; }
    public string HandlerName { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
}

public enum JobStatus
{
    Pending,
    Running,
    Succeeded,
    Dead
}

public sealed class JobRecord
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public JobStatus Status { get; set; }
    public int Attempts { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string? LastError { get; set; }
}

public sealed class AiUsageRecord
{
    public Guid Id { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public bool CacheHit { get; set; }
    public int InputTokensEst { get; set; }
    public int OutputTokensEst { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AiCacheRecord
{
    public string Hash { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string ResponsePath { get; set; } = string.Empty;
    public int HitCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastHitAtUtc { get; set; }
}

public sealed class MediaUsageRecord
{
    public Guid Id { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool CacheHit { get; set; }
    public int Bytes { get; set; }
    public int DurationMs { get; set; }
    public Guid? ProductId { get; set; } // proveniência (Fase 3B): produto que originou a imagem
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class MediaCacheRecord
{
    public string Hash { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int HitCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastHitAtUtc { get; set; }
}

public sealed class SettingRecord
{
    public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Última execução de cada job agendado — base do catch-up pós-restart.</summary>
public sealed class JobRunLogRecord
{
    public string JobName { get; set; } = string.Empty;
    public DateTime LastRunAtUtc { get; set; }
    public string? Detail { get; set; }
}
