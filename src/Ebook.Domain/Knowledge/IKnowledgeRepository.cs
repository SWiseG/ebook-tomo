namespace Ebook.Domain.Knowledge;

public interface IKnowledgeRepository
{
    /// <summary>Pacote de conhecimento já indexado para o nicho (reuso sem nova IA), se houver.</summary>
    Task<KnowledgeAsset?> GetPackByNicheAsync(Guid nicheId, CancellationToken ct = default);

    /// <summary>Asset mais recente de um tipo para o nicho (ex.: playbook MediaStyle do E15).</summary>
    Task<KnowledgeAsset?> GetLatestByTypeAsync(Guid nicheId, KnowledgeAssetType type, CancellationToken ct = default);

    void Add(KnowledgeAsset asset);
}
