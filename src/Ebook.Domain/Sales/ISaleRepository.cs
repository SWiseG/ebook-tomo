namespace Ebook.Domain.Sales;

public interface ISaleRepository
{
    Task<bool> ExistsByOrderIdAsync(string kiwifyOrderId, CancellationToken ct = default);

    void Add(SaleEvent sale);
}
