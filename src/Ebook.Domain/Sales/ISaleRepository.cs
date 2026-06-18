namespace Ebook.Domain.Sales;

public interface ISaleRepository
{
    /// <summary>
    /// Idempotência por (order_id, tipo): a Kiwify reusa o mesmo order_id na venda e no
    /// estorno/chargeback, então o tipo faz parte da chave natural — senão o estorno seria descartado.
    /// </summary>
    Task<bool> ExistsAsync(string kiwifyOrderId, SaleType type, CancellationToken ct = default);

    void Add(SaleEvent sale);
}
