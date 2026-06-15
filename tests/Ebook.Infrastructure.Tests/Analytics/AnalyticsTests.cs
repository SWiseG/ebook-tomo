using Ebook.Application.Analytics;
using Ebook.Domain.Analytics;
using Ebook.Domain.Products;
using Ebook.Domain.Sales;
using Ebook.Infrastructure.Analytics;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ebook.Infrastructure.Tests.Analytics;

/// <summary>E11 — pixel/clique → AnalyticsEvent, agregação diária idempotente em MetricDaily, funil.</summary>
public class AnalyticsTests
{
    private static ServiceProvider Build() => TestHost.Build(s =>
    {
        s.AddScoped<IAnalyticsRecorder, AnalyticsRecorder>();
        s.AddScoped<IMetricsAggregator, MetricsAggregator>();
        s.AddScoped<IMetricsReader, MetricsReader>();
    });

    private static async Task<(Guid Id, string Slug)> SeedProductAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = Product.Create(Guid.NewGuid(), "guia-metricas", "Guia", QualityTier.Commercial, DateTime.UtcNow);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Id, product.Slug);
    }

    private static async Task RecordAsync(ServiceProvider provider, AnalyticsHit hit)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAnalyticsRecorder>().RecordAsync(hit);
    }

    private static async Task SeedSaleAsync(ServiceProvider provider, Guid productId, string utm, decimal net)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        db.SaleEvents.Add(SaleEvent.Create(
            productId, $"order-{Guid.NewGuid():N}", SaleType.Sale, net + 3m, net, "BRL",
            utm, "lancamento", DateTime.UtcNow, "sales/x.json"));
        await db.SaveChangesAsync();
    }

    private static async Task<int> AggregateAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMetricsAggregator>()
            .AggregateAsync(DateTime.UtcNow.Date);
    }

    private static async Task SeedTrafficAsync(ServiceProvider provider, Guid id, string slug)
    {
        await RecordAsync(provider, new AnalyticsHit(slug, AnalyticsEventType.Visit, "instagram", "c", "launch"));
        await RecordAsync(provider, new AnalyticsHit(slug, AnalyticsEventType.Visit, "Instagram", "c", "launch"));
        await RecordAsync(provider, new AnalyticsHit(slug, AnalyticsEventType.Visit, "ig", "c", "value"));
        await RecordAsync(provider, new AnalyticsHit(slug, AnalyticsEventType.CheckoutClick, "instagram", "c", "offer"));
        await RecordAsync(provider, new AnalyticsHit(slug, AnalyticsEventType.Visit, null, null, null)); // Direct
        await SeedSaleAsync(provider, id, "instagram", 27m);
    }

    [Fact]
    public async Task Agrega_eventos_e_vendas_em_metric_daily_por_canal()
    {
        using var provider = Build();
        var (id, slug) = await SeedProductAsync(provider);
        await SeedTrafficAsync(provider, id, slug);

        var rows = await AggregateAsync(provider);
        Assert.Equal(2, rows); // Instagram + Direct

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var metrics = await db.MetricDailies.AsNoTracking().ToListAsync();

        var ig = metrics.Single(m => m.Channel == AnalyticsChannel.Instagram);
        Assert.Equal(3, ig.Visits);
        Assert.Equal(1, ig.CheckoutClicks);
        Assert.Equal(1, ig.Sales);
        Assert.Equal(27m, ig.Revenue);
        Assert.Equal(Math.Round(1d / 3d, 4), ig.ConversionRate);

        var direct = metrics.Single(m => m.Channel == AnalyticsChannel.Direct);
        Assert.Equal(1, direct.Visits);
        Assert.Equal(0, direct.Sales);
    }

    [Fact]
    public async Task Reagregar_e_idempotente_nao_duplica()
    {
        using var provider = Build();
        var (id, slug) = await SeedProductAsync(provider);
        await SeedTrafficAsync(provider, id, slug);

        await AggregateAsync(provider);
        await AggregateAsync(provider); // segunda passada (upsert, não soma)

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(2, await db.MetricDailies.AsNoTracking().CountAsync());
        var ig = await db.MetricDailies.AsNoTracking().SingleAsync(m => m.Channel == AnalyticsChannel.Instagram);
        Assert.Equal(3, ig.Visits); // não dobrou
        Assert.Equal(1, ig.Sales);
    }

    [Fact]
    public async Task Reader_devolve_funil_total_e_por_canal()
    {
        using var provider = Build();
        var (id, slug) = await SeedProductAsync(provider);
        await SeedTrafficAsync(provider, id, slug);
        await AggregateAsync(provider);

        using var scope = provider.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IMetricsReader>();
        var metrics = await reader.GetProductAsync(id, DateTime.UtcNow.Date.AddDays(-30), CancellationToken.None);

        Assert.Equal(4, metrics.Total.Visits); // 3 instagram + 1 direct
        Assert.Equal(1, metrics.Total.CheckoutClicks);
        Assert.Equal(1, metrics.Total.Sales);
        Assert.Equal(27m, metrics.Total.Revenue);
        Assert.Equal(2, metrics.ByChannel.Count);
        Assert.Equal("Instagram", metrics.ByChannel[0].Channel); // ordenado por visitas desc
    }
}
