using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Discovery;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Optimization;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Optimization;

/// <summary>
/// Executa a ação concreta de uma decisão aprovada do otimizador:
/// Kill (E12-02) arquiva e, se o portfólio cair abaixo do mínimo, dispara reposição de nicho;
/// Iterate (E12-03) coloca em iteração e aplica o novo preço sugerido; Scale/Keep só registram.
/// </summary>
public sealed class OptimizationExecutor(
    IProductRepository products,
    IJobQueue jobQueue,
    ISettingsStore settings,
    IClock clock,
    ILogger<OptimizationExecutor> logger) : IOptimizationExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result> ExecuteAsync(OptimizationDecision decision, CancellationToken ct = default)
    {
        var product = await products.GetByIdAsync(decision.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(OptimizationErrors.DecisionNotFound(decision.Id));
        }

        switch (decision.Decision)
        {
            case OptimizationDecisionKind.Kill:
                var retired = product.Retire("ROI: produto sem desempenho", clock.UtcNow);
                if (retired.IsFailure)
                {
                    return retired;
                }

                await ReplenishIfBelowMinimumAsync(ct);
                break;

            case OptimizationDecisionKind.Iterate:
                var iterating = product.StartIteration();
                if (iterating.IsFailure)
                {
                    return iterating;
                }

                ApplySuggestedPrice(product, decision.ActionsJson);
                product.CompleteIteration();

                // docs/17 P3-11: nova onda de divulgação (calendário social) ao iterar. Chave por
                // decisão p/ não colidir com o calendário original.
                await jobQueue.EnqueueAsync(new JobRequest(
                    Social.SocialJobs.Calendar,
                    JsonSerializer.Serialize(new Social.CalendarJobPayload(product.Id), JsonOptions),
                    $"social-calendar:iterate:{decision.Id}",
                    ProductId: product.Id), ct);
                break;

            case OptimizationDecisionKind.Scale:
            case OptimizationDecisionKind.Keep:
            default:
                break; // sem mudança de estado; a decisão fica registrada
        }

        var marked = decision.MarkExecuted();
        if (marked.IsFailure)
        {
            return marked;
        }

        logger.LogInformation("Decisão {Decision} executada para o produto {ProductId}",
            decision.Decision, decision.ProductId);
        return Result.Success();
    }

    private async Task ReplenishIfBelowMinimumAsync(CancellationToken ct)
    {
        var min = await settings.GetOrDefaultAsync(SettingKeys.MinActiveProducts, 10, ct);
        var activeAfterRetire = await products.CountByStatusAsync(ProductStatus.Synchronized, ct) - 1; // este ainda não foi salvo
        if (activeAfterRetire >= min)
        {
            return;
        }

        var cycle = (clock.UtcNow.Year - 2026) * 12 + clock.UtcNow.Month;
        await jobQueue.EnqueueAsync(new JobRequest(
            DiscoveryJobs.Discover,
            JsonSerializer.Serialize(new DiscoverNichesJobPayload(), JsonOptions),
            $"replenish:{cycle}"), ct);

        logger.LogInformation("Portfólio abaixo do mínimo ({Min}); reposição de nichos enfileirada", min);
    }

    private static void ApplySuggestedPrice(Product product, string actionsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(actionsJson);
            if (doc.RootElement.TryGetProperty("suggestedPrice", out var el)
                && el.TryGetDecimal(out var price)
                && price > 0)
            {
                product.SetPricing(price, product.Currency);
            }
        }
        catch (JsonException)
        {
            // sem ação de preço
        }
    }
}
