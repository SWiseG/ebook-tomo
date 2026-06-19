using System.Collections.Concurrent;

namespace Ebook.Api.Observability;

public sealed record LogEntry(long Id, DateTimeOffset Timestamp, string Level, string Message, string? Source);

/// <summary>Buffer circular em memória (até 1000 entradas) alimentado pelo SerilogMemorySink.</summary>
public sealed class InMemoryLogBuffer
{
    private const int Capacity = 1000;
    private long _counter;
    private readonly ConcurrentQueue<LogEntry> _queue = new();

    public void Add(string level, string message, string? source)
    {
        var id = Interlocked.Increment(ref _counter);
        _queue.Enqueue(new LogEntry(id, DateTimeOffset.UtcNow, level, message, source));
        // Remove entradas excedentes (FIFO)
        while (_queue.Count > Capacity) _queue.TryDequeue(out _);
    }

    public IReadOnlyList<LogEntry> GetAfter(long afterId, int limit = 200)
    {
        return _queue
            .Where(e => e.Id > afterId)
            .OrderBy(e => e.Id)
            .Take(limit)
            .ToList();
    }

    public long LastId => _counter;
}
