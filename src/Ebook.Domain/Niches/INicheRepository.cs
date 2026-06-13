namespace Ebook.Domain.Niches;

public interface INicheRepository
{
    Task<Niche?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Niche?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Slugs dos nichos já ativos/selecionados — base da afinidade histórica do score.</summary>
    Task<IReadOnlyList<string>> ActiveSlugsAsync(CancellationToken ct = default);

    void Add(Niche niche);
}

public interface ITrendSnapshotRepository
{
    void Add(TrendSnapshot snapshot);
}
