using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;

namespace Ebook.Application.Analytics;

public sealed record FunnelDto(
    int Visits,
    int CheckoutClicks,
    int Sales,
    decimal Revenue,
    double ConversionRate);

public sealed record ChannelMetricDto(
    string Channel,
    int Visits,
    int CheckoutClicks,
    int Sales,
    decimal Revenue);

public sealed record ProductMetricsDto(
    FunnelDto Total,
    IReadOnlyList<ChannelMetricDto> ByChannel);

/// <summary>Leitura do funil para o painel; implementada na Infrastructure (consulta MetricDaily).</summary>
public interface IMetricsReader
{
    Task<FunnelDto> GetOverallAsync(DateTime fromUtc, CancellationToken ct);
    Task<ProductMetricsDto> GetProductAsync(Guid productId, DateTime fromUtc, CancellationToken ct);
}

/// <summary>Funil de um produto nos últimos 30 dias (visitas → cliques → vendas).</summary>
public sealed record GetProductMetricsQuery(Guid ProductId) : IQuery<ProductMetricsDto>;

public sealed class GetProductMetricsQueryHandler(IMetricsReader reader)
    : IQueryHandler<GetProductMetricsQuery, ProductMetricsDto>
{
    public async Task<Result<ProductMetricsDto>> HandleAsync(GetProductMetricsQuery query, CancellationToken ct)
    {
        var from = DateTime.UtcNow.Date.AddDays(-30);
        return Result.Success(await reader.GetProductAsync(query.ProductId, from, ct));
    }
}
