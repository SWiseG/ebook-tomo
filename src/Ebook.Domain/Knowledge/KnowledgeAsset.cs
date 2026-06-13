using Ebook.Domain.Common;

namespace Ebook.Domain.Knowledge;

public enum KnowledgeAssetType
{
    RawResearch,
    KnowledgePack,
    Summary
}

/// <summary>
/// Índice (em SQLite) de um insumo de conhecimento cujo conteúdo vive no FileStore.
/// Reaproveitável entre produtos do mesmo nicho — cada reuso incrementa <see cref="ReuseCount"/>
/// e evita uma nova chamada de IA.
/// </summary>
public sealed class KnowledgeAsset : Entity
{
    private KnowledgeAsset()
    {
        Topic = string.Empty;
        KeywordsCsv = string.Empty;
        Path = string.Empty;
        Hash = string.Empty;
    }

    public Guid NicheId { get; private set; }
    public KnowledgeAssetType Type { get; private set; }
    public string Topic { get; private set; }
    public string KeywordsCsv { get; private set; }
    public string Path { get; private set; }
    public string Hash { get; private set; }
    public int ReuseCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static KnowledgeAsset Create(
        Guid nicheId,
        KnowledgeAssetType type,
        string topic,
        string keywordsCsv,
        string path,
        string hash,
        DateTime utcNow) =>
        new()
        {
            NicheId = nicheId,
            Type = type,
            Topic = topic,
            KeywordsCsv = keywordsCsv,
            Path = path,
            Hash = hash,
            ReuseCount = 0,
            CreatedAtUtc = utcNow
        };

    public void MarkReused() => ReuseCount++;
}
