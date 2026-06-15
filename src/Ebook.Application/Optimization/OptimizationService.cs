using System.Text.Json;
using Ebook.Application.Analytics;
using Ebook.Application.Common.Settings;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Optimization;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Optimization;

/// <summary>
/// Ciclo de otimização de 30 dias (E12-01): para cada produto Live, lê o funil (E11),
/// classifica (RoiClassifier) e propõe uma decisão. Idempotente por ciclo. As decisões ficam
/// Proposed para veto humano (E12-05), salvo se <c>roi.autoExecute</c> estiver ligado.
/// </summary>
public sealed class OptimizationService(
    IOptimizationRepository optimization,
    IProductRepository products,
    IMetricsReader metrics,
    IOptimizationExecutor executor,
    ISettingsStore settings,
    IFileStore fileStore,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<OptimizationService> logger) : IOptimizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<Guid>> RunCycleAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var cycle = (now.Year - 2026) * 12 + now.Month;

        var existing = await optimization.GetRunByCycleAsync(cycle, ct);
        if (existing is not null)
        {
            return Result.Success(existing.Id); // ciclo já rodado
        }

        var thresholds = await settings.GetOrDefaultAsync(SettingKeys.RoiThresholds, RoiThresholds.Default, ct);
        var from = now.Date.AddDays(-30);

        var run = OptimizationRun.Start(cycle, now);
        optimization.AddRun(run);

        var live = await products.ListByStatusAsync(ProductStatus.Live, ct);
        var decisions = new List<OptimizationDecision>(live.Count);

        foreach (var product in live)
        {
            var funnel = (await metrics.GetProductAsync(product.Id, from, ct)).Total;
            var perf = new ProductPerformance(
                funnel.Visits, funnel.CheckoutClicks, funnel.Sales, funnel.Revenue, funnel.ConversionRate);
            var verdict = RoiClassifier.Classify(perf, thresholds);

            var rationale = JsonSerializer.Serialize(new { verdict.Rationale, perf }, JsonOptions);
            var actions = BuildActions(verdict.Decision, product);
            var decision = OptimizationDecision.Propose(run.Id, product.Id, verdict.Decision, rationale, actions);
            optimization.AddDecision(decision);
            decisions.Add(decision);
        }

        var reportPath = OptimizationPaths.Report(cycle);
        await fileStore.WriteTextAsync(reportPath, BuildReport(cycle, now, live, decisions), ct);
        run.Complete(reportPath);

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Ciclo de otimização {Cycle}: {Count} decisões propostas", cycle, decisions.Count);

        var autoExecute = await settings.GetOrDefaultAsync(SettingKeys.RoiAutoExecute, false, ct);
        if (autoExecute)
        {
            await AutoExecuteAsync(decisions, ct);
        }

        return Result.Success(run.Id);
    }

    private async Task AutoExecuteAsync(IReadOnlyList<OptimizationDecision> decisions, CancellationToken ct)
    {
        foreach (var decision in decisions)
        {
            if (decision.Approve().IsFailure)
            {
                continue;
            }

            var executed = await executor.ExecuteAsync(decision, ct);
            if (executed.IsFailure)
            {
                logger.LogWarning("Falha ao executar decisão {Id}: {Error}", decision.Id, executed.Error.Code);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private static string BuildActions(OptimizationDecisionKind kind, Product product) => kind switch
    {
        OptimizationDecisionKind.Iterate => JsonSerializer.Serialize(new
        {
            suggestedPrice = Math.Round(product.Price * 0.85m, 2),
            refreshLandingPage = true,
            refreshSocialCalendar = true
        }, JsonOptions),
        OptimizationDecisionKind.Kill => JsonSerializer.Serialize(new { archive = true, replenish = true }, JsonOptions),
        OptimizationDecisionKind.Scale => JsonSerializer.Serialize(new { increaseBudget = true }, JsonOptions),
        _ => "{}"
    };

    private static string BuildReport(
        int cycle, DateTime now, IReadOnlyList<Product> live, IReadOnlyList<OptimizationDecision> decisions)
    {
        var bySlug = live.ToDictionary(p => p.Id, p => p.Slug);
        return JsonSerializer.Serialize(new
        {
            cycle,
            period = new { from = now.Date.AddDays(-30), to = now },
            portfolio = new { active = live.Count, revenue = live.Sum(p => p.Price) },
            decisions = decisions.Select(d => new
            {
                product = bySlug.GetValueOrDefault(d.ProductId, d.ProductId.ToString()),
                decision = d.Decision.ToString()
            })
        }, JsonOptions);
    }
}
