namespace Ebook.Domain.Social;

public interface IChannelRepository
{
    void Add(Channel channel);

    Task<Channel?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Canal do nicho (1 por nicho). Usado para rotear a publicação dos produtos do nicho.</summary>
    Task<Channel?> GetByNicheAsync(Guid nicheId, CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> ListAsync(CancellationToken ct = default);
}
