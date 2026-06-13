using Ebook.Domain.Common;

namespace Ebook.Domain.Products;

public enum ArtifactType
{
    Manuscript,
    Pdf,
    Cover,
    Mockup,
    LpBundle,
    SocialCard,
    Video
}

/// <summary>
/// Índice de um artefato gerado de um produto. Conteúdo no FileStore (<see cref="Path"/>),
/// integridade por <see cref="Hash"/>; versionado para permitir regerações.
/// </summary>
public sealed class Artifact : Entity
{
    private Artifact()
    {
        Path = string.Empty;
        Hash = string.Empty;
        MetaJson = "{}";
    }

    public Guid ProductId { get; private set; }
    public ArtifactType Type { get; private set; }
    public string Path { get; private set; }
    public string Hash { get; private set; }
    public int Version { get; private set; }
    public string MetaJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Artifact Create(
        Guid productId,
        ArtifactType type,
        string path,
        string hash,
        int version,
        string metaJson,
        DateTime utcNow) =>
        new()
        {
            ProductId = productId,
            Type = type,
            Path = path,
            Hash = hash,
            Version = version,
            MetaJson = metaJson,
            CreatedAtUtc = utcNow
        };
}
