using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Text;
using Ebook.Application.Content;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Knowledge;

/// <summary>
/// E15-01/02/03 — Loop de aprendizado de estilo. Analisa a capa do produto representativo de um nicho
/// (via <see cref="IStyleAnalyzer"/> / Claude vision) e grava um <c>KnowledgeAsset(MediaStyle)</c> — o
/// "playbook" de estilo do nicho. Re-entrante: pula quando já há playbook recente (&lt; 7 dias).
/// Falha de visão é suave (loga e retorna sucesso) — o cron tenta de novo na próxima janela.
/// </summary>
public sealed class StyleLearnJobHandler(
    IProductRepository products,
    INicheRepository niches,
    IArtifactStore artifactStore,
    IStyleAnalyzer analyzer,
    IKnowledgeRepository knowledge,
    IFileStore fileStore,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<StyleLearnJobHandler> logger) : IJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Freshness = TimeSpan.FromDays(7);

    public string Type => KnowledgeJobs.StyleLearn;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<StyleLearnJobPayload>(payloadJson, JsonOptions)!;

        var existing = await knowledge.GetLatestByTypeAsync(payload.NicheId, KnowledgeAssetType.MediaStyle, ct);
        if (existing is not null && clock.UtcNow - existing.CreatedAtUtc < Freshness)
        {
            existing.MarkReused();
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(); // playbook recente — nada a reaprender esta semana
        }

        var product = await products.GetByIdAsync(payload.ProductId, ct);
        var niche = await niches.GetByIdAsync(payload.NicheId, ct);
        if (product is null || niche is null)
        {
            return Result.Success(); // produto/nicho sumiu — nada a aprender
        }

        var cover = await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug), ct);
        if (cover is null || cover.Length == 0)
        {
            logger.LogInformation("Style learn: nicho {Slug} sem capa para analisar; pulando", niche.Slug);
            return Result.Success();
        }

        var analysis = await analyzer.AnalyzeAsync(cover, niche.Name, ct);
        if (analysis.IsFailure)
        {
            // visão indisponível/instável: não vira dead-letter; o cron tenta na próxima janela
            logger.LogWarning("Style learn: análise de visão falhou para {Slug}: {Error}", niche.Slug, analysis.Error);
            return Result.Success();
        }

        var parsed = AiJson.Parse<MediaStyleDto>(analysis.Value, "style.analyze");
        if (parsed.IsFailure || string.IsNullOrWhiteSpace(parsed.Value.Summary))
        {
            logger.LogWarning("Style learn: saída de visão inválida para {Slug}", niche.Slug);
            return Result.Success();
        }

        var content = analysis.Value;
        var hash = Hash(content);
        var path = $"niches/{niche.Slug}/knowledge/style/{hash}.style.json";
        await fileStore.WriteTextAsync(path, content, ct);

        var keywords = parsed.Value.PromptHints is { Count: > 0 } hints ? string.Join(',', hints) : string.Empty;
        knowledge.Add(KnowledgeAsset.Create(
            niche.Id, KnowledgeAssetType.MediaStyle, "media style", keywords, path, hash, clock.UtcNow));
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Style learn: playbook de estilo gravado para o nicho {Slug}", niche.Slug);
        return Result.Success();
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
