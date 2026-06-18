using System.Globalization;
using System.Text.Json;
using Ebook.Domain.Common;
using Ebook.Domain.Sales;

namespace Ebook.Application.Publishing;

/// <summary>
/// Mapeia o JSON cru do webhook da Kiwify para um <see cref="RecordSaleCommand"/>.
///
/// Estrutura real da Kiwify (conferida contra a doc e integradores públicos, jun/2026):
/// - identificadores e status no topo: <c>order_id</c> / <c>order_ref</c>,
///   <c>order_status</c> e/ou <c>webhook_event_type</c>;
/// - valores em <b>centavos</b> (string, ex.: "8063" = R$ 80,63) dentro do objeto
///   <c>Commissions</c>: <c>charge_amount</c> (bruto), <c>my_commission</c> (líquido do produtor), <c>currency</c>;
/// - produto em <c>Product.product_id</c>; UTMs em <c>TrackingParameters</c>.
/// Mantém fallbacks planos (valores em reais) por robustez/compatibilidade.
///
/// Só grava eventos de pagamento confirmado/estorno/chargeback. Eventos pendentes
/// (pix/boleto gerado, aguardando pagamento, recusado, carrinho abandonado) são reconhecidos
/// e ignorados — devolvem sucesso com valor nulo (o endpoint responde 200 sem gravar), evitando
/// venda-fantasma já que esses eventos também carregam <c>Commissions.charge_amount</c>.
/// O payload bruto é sempre persistido (no handler) para auditoria/reprocessamento.
/// </summary>
public static class KiwifyWebhookMapper
{
    /// <summary>
    /// Resultado de falha = JSON inválido / sem order_id. Sucesso com valor nulo = evento
    /// reconhecido porém não gravável (pendente/recusado). Sucesso com valor = venda/estorno a gravar.
    /// </summary>
    public static Result<RecordSaleCommand?> Map(string rawJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson);
        }
        catch (JsonException ex)
        {
            return Result.Failure<RecordSaleCommand?>(new Error("Webhook.InvalidJson", ex.Message));
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<RecordSaleCommand?>(new Error("Webhook.InvalidJson", "Payload não é um objeto."));
            }

            var orderId = FindString(root, "order_id", "order_ref", "orderId", "id");
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Result.Failure<RecordSaleCommand?>(new Error("Webhook.MissingOrderId", "order_id ausente."));
            }

            var status = FindString(root, "order_status", "webhook_event_type", "status", "event");
            var type = ClassifySale(status);
            if (type is null)
            {
                // evento reconhecido mas não-gravável (pendente/recusado/abandonado): 200 OK sem gravar
                return Result.Success<RecordSaleCommand?>(null);
            }

            // objetos aninhados (CapitalCase na Kiwify; aceita minúsculas por robustez)
            var commissions = GetObject(root, "Commissions", "commissions");
            var product = GetObject(root, "Product", "product");
            var tracking = GetObject(root, "TrackingParameters", "tracking_parameters", "trackingParameters");

            // valores: Kiwify manda centavos (string) em Commissions; fallback plano em reais
            var gross = FindCents(commissions, "charge_amount", "product_base_price")
                        ?? FindReais(root, "charge_amount", "amount", "total")
                        ?? 0m;
            var net = FindCents(commissions, "my_commission", "settlement_amount")
                      ?? FindReais(root, "commissioned_value", "net_amount", "net")
                      ?? gross;
            var currency = FindString(commissions, "currency")
                           ?? FindString(root, "currency", "currency_code")
                           ?? "BRL";
            var productRef = FindString(product, "product_id", "id")
                             ?? FindString(root, "product_id", "productId", "kiwify_product_id");
            var utmSource = FindString(tracking, "utm_source", "src")
                            ?? FindString(root, "utm_source", "src");
            var utmCampaign = FindString(tracking, "utm_campaign")
                              ?? FindString(root, "utm_campaign", "campaign");
            var occurredAt = FindDateTime(root, "approved_date", "created_at", "updated_at") ?? DateTime.UtcNow;

            return Result.Success<RecordSaleCommand?>(new RecordSaleCommand(
                orderId!,
                type.Value,
                gross,
                net,
                currency,
                productRef,
                utmSource,
                utmCampaign,
                occurredAt,
                rawJson));
        }
    }

    /// <summary>
    /// Classifica o status num tipo gravável. <c>null</c> = evento não-gravável (pendente/recusado/
    /// abandonado/gerado). Status vazio assume venda (compatibilidade).
    /// </summary>
    private static SaleType? ClassifySale(string? status)
    {
        var s = (status ?? string.Empty).ToLowerInvariant();
        if (s.Length == 0)
        {
            return SaleType.Sale;
        }

        if (s.Contains("refund") || s.Contains("reembols"))
        {
            return SaleType.Refund;
        }

        if (s.Contains("chargeback") || s.Contains("chargedback"))
        {
            return SaleType.Chargeback;
        }

        if (s.Contains("paid") || s.Contains("approv") || s.Contains("aprovad"))
        {
            return SaleType.Sale;
        }

        // waiting_payment, pix/boleto gerado, refused/recusado, carrinho abandonado, etc. → não grava
        return null;
    }

    private static JsonElement? GetObject(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Object)
            {
                return el;
            }
        }

        return null;
    }

    private static string? FindString(JsonElement? obj, params string[] keys)
    {
        if (obj is not { } el)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (!el.TryGetProperty(key, out var v))
            {
                continue;
            }

            var s = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>Lê um valor monetário em centavos (string "8063" ou número inteiro) → reais.</summary>
    private static decimal? FindCents(JsonElement? obj, params string[] keys)
    {
        if (obj is not { } el)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (!el.TryGetProperty(key, out var v))
            {
                continue;
            }

            switch (v.ValueKind)
            {
                case JsonValueKind.String:
                    var raw = v.GetString();
                    // dígitos puros = centavos; com separador = já em reais
                    if (decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cents))
                    {
                        return cents / 100m;
                    }

                    if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var reais))
                    {
                        return reais;
                    }

                    break;

                case JsonValueKind.Number when v.TryGetDecimal(out var num):
                    // inteiro = centavos; com fração = já em reais
                    return num == Math.Truncate(num) ? num / 100m : num;
            }
        }

        return null;
    }

    /// <summary>Lê um valor monetário plano já em reais (fallback de compatibilidade).</summary>
    private static decimal? FindReais(JsonElement? obj, params string[] keys)
    {
        if (obj is not { } el)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (!el.TryGetProperty(key, out var v))
            {
                continue;
            }

            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
            {
                return d;
            }

            if (v.ValueKind == JsonValueKind.String
                && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds))
            {
                return ds;
            }
        }

        return null;
    }

    private static DateTime? FindDateTime(JsonElement? obj, params string[] keys)
    {
        if (obj is not { } el)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (el.TryGetProperty(key, out var v)
                && v.ValueKind == JsonValueKind.String
                && DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            {
                return dt;
            }
        }

        return null;
    }
}
