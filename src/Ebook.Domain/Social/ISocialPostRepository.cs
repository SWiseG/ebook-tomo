namespace Ebook.Domain.Social;

public interface ISocialPostRepository
{
    void Add(SocialPost post);

    Task<SocialPost?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Há posts já planejados para o produto? (idempotência da geração do calendário)</summary>
    Task<bool> ExistsForProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Posts vencidos e ainda planejados (prontos para publicar). Com <paramref name="approvedOnly"/>
    /// só retorna os aprovados no painel (gate); sem ele, todos os vencidos (modo auto-aprovar).
    /// </summary>
    Task<IReadOnlyList<SocialPost>> GetDueAsync(DateTime nowUtc, int take, bool approvedOnly, CancellationToken ct = default);

    Task<IReadOnlyList<SocialPost>> GetByProductAsync(Guid productId, CancellationToken ct = default);
}
