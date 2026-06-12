namespace Ebook.Domain.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Persiste alterações e serializa Domain Events dos agregados rastreados
    /// para o Outbox na mesma transação.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
