using Ebook.Application;
using Ebook.Application.Analytics;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Settings;
using Ebook.Application.Optimization;
using Ebook.Domain.Analytics;
using Ebook.Domain.Optimization;
using Ebook.Domain.Products;
using Ebook.Infrastructure.Analytics;
using Ebook.Infrastructure.Jobs;
using Ebook.Infrastructure.Optimization;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Settings;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ebook.Infrastructure.Tests.Optimization;

/// <summary>E12 — ciclo de otimização: classificação por desempenho, veto/aprovação, kill/iterate.</summary>
public class OptimizationTests
{
    private static ServiceProvider Build() => TestHost.Build(s =>
    {
        s.AddApplication(); // IOptimizationService/Executor + command/query handlers + dispatcher
        s.AddScoped<IOptimizationRepository, OptimizationRepository>();
        s.AddScoped<IOptimizationReader, OptimizationReader>();
        s.AddScoped<IMetricsReader, MetricsReader>();
        s.AddScoped<IProductRepository, ProductRepository>();
        s.AddScoped<ISettingsStore, SettingsStore>();
        s.AddScoped<IJobQueue, JobQueue>();
    });

    private static async Task<Guid> SeedLiveProductAsync(ServiceProvider provider, string slug, decimal price)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var now = DateTime.UtcNow;
        var product = Product.Create(Guid.NewGuid(), slug, $"Produto {slug}", QualityTier.Commercial, now);
        product.SetPricing(price, "BRL");
        product.AdvanceStage();
        product.AdvanceStage();
        product.AdvanceStage();
        product.AdvanceStage(); // → Lp
        product.SubmitForApproval();
        product.Approve(); // → Publishing
        product.MarkPublished($"kw-{slug}", $"https://pay/{slug}", $"/lp/{slug}", now); // → Live
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task SeedMetricsAsync(ServiceProvider provider, Guid productId, int visits, int sales, decimal revenue)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var m = MetricDaily.Create(productId, DateTime.UtcNow.Date, AnalyticsChannel.Instagram);
        m.Set(visits, 0, sales, revenue);
        db.MetricDailies.Add(m);
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> RunCycleAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IOptimizationService>().RunCycleAsync();
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task SetAutoExecuteAsync(ServiceProvider provider, bool value)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SetAsync(SettingKeys.RoiAutoExecute, value);
    }

    private static async Task SendAsync(ServiceProvider provider, ICommand<bool> command)
    {
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
    }

    private static async Task<OptimizationDecision> DecisionForAsync(ServiceProvider provider, Guid productId)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        return await db.OptimizationDecisions.AsNoTracking().FirstAsync(d => d.ProductId == productId);
    }

    [Fact]
    public async Task Classifica_produtos_por_desempenho_e_propoe_decisoes()
    {
        using var provider = Build();
        var scale = await SeedLiveProductAsync(provider, "scale", 30m);
        var kill = await SeedLiveProductAsync(provider, "kill", 30m);
        var iterate = await SeedLiveProductAsync(provider, "iterate", 30m);
        await SeedMetricsAsync(provider, scale, visits: 300, sales: 15, revenue: 450m);
        await SeedMetricsAsync(provider, kill, visits: 300, sales: 0, revenue: 0m);
        await SeedMetricsAsync(provider, iterate, visits: 300, sales: 2, revenue: 60m);

        await RunCycleAsync(provider);

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(1, await db.OptimizationRuns.CountAsync());
        Assert.Equal(3, await db.OptimizationDecisions.CountAsync());
        Assert.All(await db.OptimizationDecisions.AsNoTracking().ToListAsync(),
            d => Assert.Equal(OptimizationDecisionStatus.Proposed, d.Status));

        Assert.Equal(OptimizationDecisionKind.Scale, (await DecisionForAsync(provider, scale)).Decision);
        Assert.Equal(OptimizationDecisionKind.Kill, (await DecisionForAsync(provider, kill)).Decision);
        Assert.Equal(OptimizationDecisionKind.Iterate, (await DecisionForAsync(provider, iterate)).Decision);

        // nada executado ainda (veto humano por padrão): produtos seguem Live
        Assert.All(await db.Products.AsNoTracking().ToListAsync(), p => Assert.Equal(ProductStatus.Live, p.Status));
    }

    [Fact]
    public async Task Ciclo_e_idempotente_por_ciclo()
    {
        using var provider = Build();
        var id = await SeedLiveProductAsync(provider, "p", 30m);
        await SeedMetricsAsync(provider, id, 300, 15, 450m);

        await RunCycleAsync(provider);
        await RunCycleAsync(provider);

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(1, await db.OptimizationRuns.CountAsync());
        Assert.Equal(1, await db.OptimizationDecisions.CountAsync());
    }

    [Fact]
    public async Task Aprovar_kill_arquiva_o_produto_e_repoe_o_portfolio()
    {
        using var provider = Build();
        var id = await SeedLiveProductAsync(provider, "kill", 30m);
        await SeedMetricsAsync(provider, id, visits: 300, sales: 0, revenue: 0m);
        await RunCycleAsync(provider);

        var decision = await DecisionForAsync(provider, id);
        await SendAsync(provider, new ApproveDecisionCommand(decision.Id));

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.Equal(ProductStatus.Retired, product.Status);
        Assert.Equal(OptimizationDecisionStatus.Executed,
            (await db.OptimizationDecisions.AsNoTracking().SingleAsync()).Status);
        // portfólio abaixo do mínimo → reposição de nichos enfileirada
        Assert.True(await db.Jobs.AnyAsync(j => j.Type == "trends.discover"));
    }

    [Fact]
    public async Task Aprovar_iterate_aplica_novo_preco_e_volta_para_live()
    {
        using var provider = Build();
        var id = await SeedLiveProductAsync(provider, "iterate", 30m);
        await SeedMetricsAsync(provider, id, visits: 300, sales: 2, revenue: 60m);
        await RunCycleAsync(provider);

        var decision = await DecisionForAsync(provider, id);
        await SendAsync(provider, new ApproveDecisionCommand(decision.Id));

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.Equal(ProductStatus.Live, product.Status); // iterou e voltou
        Assert.Equal(25.5m, product.Price); // 30 * 0.85
    }

    [Fact]
    public async Task Vetar_nao_executa_acao()
    {
        using var provider = Build();
        var id = await SeedLiveProductAsync(provider, "kill", 30m);
        await SeedMetricsAsync(provider, id, visits: 300, sales: 0, revenue: 0m);
        await RunCycleAsync(provider);

        var decision = await DecisionForAsync(provider, id);
        await SendAsync(provider, new VetoDecisionCommand(decision.Id));

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(ProductStatus.Live, (await db.Products.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(OptimizationDecisionStatus.Vetoed,
            (await db.OptimizationDecisions.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Modo_auto_executa_as_decisoes_no_ciclo()
    {
        using var provider = Build();
        var id = await SeedLiveProductAsync(provider, "kill", 30m);
        await SeedMetricsAsync(provider, id, visits: 300, sales: 0, revenue: 0m);
        await SetAutoExecuteAsync(provider, true);

        await RunCycleAsync(provider);

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(ProductStatus.Retired, (await db.Products.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(OptimizationDecisionStatus.Executed,
            (await db.OptimizationDecisions.AsNoTracking().SingleAsync()).Status);
    }
}
