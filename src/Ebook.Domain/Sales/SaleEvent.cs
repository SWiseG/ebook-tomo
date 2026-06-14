using Ebook.Domain.Common;

namespace Ebook.Domain.Sales;

public enum SaleType
{
    Sale,
    Refund,
    Chargeback
}

/// <summary>
/// Venda/estorno registrado a partir de um webhook da Kiwify (E07-02).
/// Chave natural <see cref="KiwifyOrderId"/> garante idempotência na reentrega.
/// O payload bruto fica no FileStore (<see cref="RawPayloadPath"/>) para auditoria.
/// </summary>
public sealed class SaleEvent : Entity
{
    private SaleEvent()
    {
        KiwifyOrderId = string.Empty;
        Currency = "BRL";
        RawPayloadPath = string.Empty;
    }

    public Guid? ProductId { get; private set; }
    public string KiwifyOrderId { get; private set; }
    public SaleType Type { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string Currency { get; private set; }
    public string? UtmSource { get; private set; }
    public string? UtmCampaign { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string RawPayloadPath { get; private set; }

    public static SaleEvent Create(
        Guid? productId,
        string kiwifyOrderId,
        SaleType type,
        decimal grossAmount,
        decimal netAmount,
        string currency,
        string? utmSource,
        string? utmCampaign,
        DateTime occurredAtUtc,
        string rawPayloadPath) =>
        new()
        {
            ProductId = productId,
            KiwifyOrderId = kiwifyOrderId,
            Type = type,
            GrossAmount = grossAmount,
            NetAmount = netAmount,
            Currency = currency,
            UtmSource = utmSource,
            UtmCampaign = utmCampaign,
            OccurredAtUtc = occurredAtUtc,
            RawPayloadPath = rawPayloadPath
        };
}
