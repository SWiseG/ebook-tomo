using Ebook.Domain.Common;

namespace Ebook.Domain.Analytics;

public enum AnalyticsChannel
{
    Direct,
    Instagram,
    Facebook,
    X
}

public enum AnalyticsEventType
{
    Visit,
    CheckoutClick
}

/// <summary>
/// Evento bruto de tráfego (E11-01): uma visita à LP (pixel) ou um clique de checkout (/go).
/// O canal é derivado do utm_source no momento da gravação. Agregado diariamente em MetricDaily.
/// </summary>
public sealed class AnalyticsEvent : Entity
{
    private AnalyticsEvent()
    {
    }

    public Guid? ProductId { get; private set; }
    public AnalyticsChannel Channel { get; private set; }
    public AnalyticsEventType Type { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? UtmSource { get; private set; }
    public string? UtmCampaign { get; private set; }
    public string? UtmContent { get; private set; }

    /// <summary>Tag da variante de LP que gerou o evento (ex.: "v1", "v2"). Null = variante única.</summary>
    public string? VariantTag { get; private set; }

    public static AnalyticsEvent Create(
        Guid? productId,
        AnalyticsEventType type,
        AnalyticsChannel channel,
        DateTime occurredAtUtc,
        string? utmSource,
        string? utmCampaign,
        string? utmContent,
        string? variantTag = null) =>
        new()
        {
            ProductId = productId,
            Type = type,
            Channel = channel,
            OccurredAtUtc = occurredAtUtc,
            UtmSource = utmSource,
            UtmCampaign = utmCampaign,
            UtmContent = utmContent,
            VariantTag = variantTag,
        };
}

/// <summary>Mapeia um utm_source (texto livre) para um canal de aquisição.</summary>
public static class ChannelMap
{
    public static AnalyticsChannel From(string? utmSource)
    {
        if (string.IsNullOrWhiteSpace(utmSource))
        {
            return AnalyticsChannel.Direct;
        }

        var s = utmSource.ToLowerInvariant();
        return s switch
        {
            _ when s.Contains("insta") || s == "ig" => AnalyticsChannel.Instagram,
            _ when s.Contains("face") || s == "fb" => AnalyticsChannel.Facebook,
            _ when s == "x" || s.Contains("twitter") => AnalyticsChannel.X,
            _ => AnalyticsChannel.Direct
        };
    }
}
