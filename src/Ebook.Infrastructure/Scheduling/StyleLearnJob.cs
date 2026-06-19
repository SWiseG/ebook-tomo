using System.Globalization;
using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Knowledge;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Ebook.Infrastructure.Scheduling;

/// <summary>
/// E15-01 — Cron semanal do loop de aprendizado de estilo. Para cada nicho com produto ativo
/// (Synchronized), enfileira um job <c>style.learn</c> que analisa a capa do produto mais recente e
/// grava o playbook de estilo do nicho. Gated por <c>style.learn.enabled</c> (default false; exige CLI
/// Claude com visão). Chave por nicho+semana evita reanalisar o mesmo nicho mais de uma vez por semana.
/// </summary>
[DisallowConcurrentExecution]
public sealed class StyleLearnJob(
    IProductRepository products,
    ISettingsStore settings,
    IJobQueue jobQueue,
    IClock clock,
    ILogger<StyleLearnJob> logger) : IJob
{
    public const string JobName = "style-learn";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        if (!await settings.GetOrDefaultAsync(SettingKeys.StyleLearnEnabled, false, ct))
        {
            return;
        }

        var week = ISOWeek.GetWeekOfYear(clock.UtcNow);
        var active = await products.ListByStatusAsync(ProductStatus.Synchronized, ct);

        // um produto representativo por nicho (o mais recente) — base do playbook de estilo do nicho
        var perNiche = active
            .GroupBy(p => p.NicheId)
            .Select(g => g.OrderByDescending(p => p.CreatedAtUtc).First());

        var count = 0;
        foreach (var product in perNiche)
        {
            await jobQueue.EnqueueAsync(new JobRequest(
                KnowledgeJobs.StyleLearn,
                JsonSerializer.Serialize(new StyleLearnJobPayload(product.NicheId, product.Id), JsonOptions),
                KnowledgeJobs.StyleLearnKey(product.NicheId, week),
                ProductId: product.Id), ct);
            count++;
        }

        logger.LogInformation("Cron de aprendizado de estilo: {Count} nicho(s) enfileirado(s) (semana {Week})", count, week);
    }
}
