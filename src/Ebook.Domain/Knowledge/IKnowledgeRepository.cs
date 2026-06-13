namespace Ebook.Domain.Knowledge;

public interface IKnowledgeRepository
{
    /// <summary>Pacote de conhecimento já indexado para o nicho (reuso sem nova IA), se houver.</summary>
    Task<KnowledgeAsset?> GetPackByNicheAsync(Guid nicheId, CancellationToken ct = default);

    void Add(KnowledgeAsset asset);
}
