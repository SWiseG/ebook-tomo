using System.Globalization;
using System.Text.Json;
using Ebook.Domain.Common;
using Ebook.Domain.Sales;

namespace Ebook.Application.Publishing;

/// <summary>
/// Mapeia o JSON cru do webhook da Kiwify para um <see cref="RecordSaleCommand"/>, de forma
/// tolerante (vários aliases de campo). Best-effort: os nomes exatos devem ser ajustados
/// contra o payload real da Kiwify — por isso o payload bruto é sempre persistido.
/// </summary>
public static class KiwifyWebhookMapper
{
    public static Result<RecordSaleCommand> Map(string rawJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson);
        }
        catch (JsonException ex)
        {
            return Result.Failure<RecordSaleCommand>(new Error("Webhook.InvalidJson", ex.Message));
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<RecordSaleCommand>(new Error("Webhook.InvalidJson", "Payload não é um objeto."));
            }

            var orderId = FindString(root, "order_id", "orderId", "order_ref", "id");
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Result.Failure<RecordSaleCommand>(new Error("Webhook.MissingOrderId", "order_id ausente."));
            }

            var status = FindString(root, "order_status", "webhook_event_type", "status", "event") ?? "paid";
            var gross = FindDecimal(root, "charge_amount", "product_base_price", "amount", "total") ?? 0m;
            var net = FindDecimal(root, "commissioned_value", "net_amount", "net") ?? gross;
            var currency = FindString(root, "currency", "currency_code") ?? "BRL";
            var productRef = FindString(root, "product_id", "productId", "kiwify_product_id");
            var utmSource = FindString(root, "utm_source", "src");
            var utmCampaign = FindString(root, "utm_campaign", "campaign");
            var occurredAt = FindDateTime(root, "created_at", "approved_date", "updated_at") ?? DateTime.UtcNow;

            return Result.Success(new RecordSaleCommand(
                orderId!,
                MapType(status),
                gross,
                net,
                currency!,
                productRef,
                utmSource,
                utmCampaign,
                occurredAt,
                rawJson));
        }
    }

    private static SaleType MapType(string status) => status.ToLowerInvariant() switch
    {
        var s when s.Contains("refund") => SaleType.Refund,
        var s when s.Contains("chargeback") || s.Contains("chargedback") => SaleType.Chargeback,
        _ => SaleType.Sale
    };

    private static string? FindString(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var el))
            {
                return el.ValueKind switch
                {
                    JsonValueKind.String => el.GetString(),
                    JsonValueKind.Number => el.GetRawText(),
                    _ => null
                };
            }
        }

        return null;
    }

    private static decimal? FindDecimal(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!root.TryGetProperty(key, out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d))
            {
                return d;
            }

            if (el.ValueKind == JsonValueKind.String
                && decimal.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds))
            {
                return ds;
            }
        }

        return null;
    }

    private static DateTime? FindDateTime(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var el)
                && el.ValueKind == JsonValueKind.String
                && DateTime.TryParse(el.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            {
                return dt;
            }
        }

        return null;
    }
}
