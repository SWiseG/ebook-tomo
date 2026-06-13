using Ebook.Domain.Common;

namespace Ebook.Domain.Niches;

public enum TrendSource
{
    GoogleTrends,
    Reddit,
    Amazon,
    Autocomplete
}

/// <summary>
/// Evidência de tendência de um nicho, coletada de uma fonte. Payload bruto no FileStore;
/// esta linha é o índice (SQLite).
/// </summary>
public sealed class TrendSnapshot : Entity
{
    private TrendSnapshot() => PayloadPath = string.Empty;

    public Guid NicheId { get; private set; }
    public TrendSource Source { get; private set; }
    public string PayloadPath { get; private set; }
    public DateTime CollectedAtUtc { get; private set; }

    public static TrendSnapshot Create(Guid nicheId, TrendSource source, string payloadPath, DateTime utcNow) =>
        new()
        {
            NicheId = nicheId,
            Source = source,
            PayloadPath = payloadPath,
            CollectedAtUtc = utcNow
        };
}
