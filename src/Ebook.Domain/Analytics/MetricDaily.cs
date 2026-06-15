using Ebook.Domain.Common;

namespace Ebook.Domain.Analytics;

/// <summary>
/// Métrica agregada por produto/dia/canal (E11-02). Reconstruída de forma idempotente pela
/// agregação diária a partir dos eventos brutos (visitas/cliques) e das vendas (SaleEvent).
/// </summary>
public sealed class MetricDaily : Entity
{
    private MetricDaily()
    {
    }

    public Guid ProductId { get; private set; }
    public DateTime DateUtc { get; private set; }
    public AnalyticsChannel Channel { get; private set; }
    public int Visits { get; private set; }
    public int CheckoutClicks { get; private set; }
    public int Sales { get; private set; }
    public decimal Revenue { get; private set; }
    public double ConversionRate { get; private set; }

    public static MetricDaily Create(Guid productId, DateTime dateUtc, AnalyticsChannel channel) =>
        new() { ProductId = productId, DateUtc = dateUtc.Date, Channel = channel };

    /// <summary>Define os totais do dia (upsert idempotente) e recalcula a conversão.</summary>
    public void Set(int visits, int checkoutClicks, int sales, decimal revenue)
    {
        Visits = visits;
        CheckoutClicks = checkoutClicks;
        Sales = sales;
        Revenue = revenue;
        ConversionRate = visits > 0 ? Math.Round((double)sales / visits, 4) : 0;
    }
}
