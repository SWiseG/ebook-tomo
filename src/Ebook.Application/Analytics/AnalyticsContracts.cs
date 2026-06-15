using Ebook.Domain.Analytics;

namespace Ebook.Application.Analytics;

/// <summary>Uma batida de tráfego vinda da LP: visita (pixel) ou clique de checkout (/go).</summary>
public sealed record AnalyticsHit(
    string Slug,
    AnalyticsEventType Type,
    string? UtmSource,
    string? UtmCampaign,
    string? UtmContent);

/// <summary>
/// Grava eventos brutos de tráfego (E11-01). Resiliente: nunca deve quebrar o pixel/redirect.
/// Implementado na Infrastructure (resolve o produto pelo slug e insere AnalyticsEvent).
/// </summary>
public interface IAnalyticsRecorder
{
    Task RecordAsync(AnalyticsHit hit, CancellationToken ct = default);
}

/// <summary>
/// Agrega os eventos brutos + vendas de um dia em MetricDaily (E11-02), de forma idempotente
/// (upsert por produto/dia/canal). Implementado na Infrastructure (consulta direta).
/// </summary>
public interface IMetricsAggregator
{
    Task<int> AggregateAsync(DateTime dateUtc, CancellationToken ct = default);
}
