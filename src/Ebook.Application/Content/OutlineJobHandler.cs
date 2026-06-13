using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Knowledge;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content;

/// <summary>
/// Etapa Outline: garante o KnowledgePack do nicho, gera o outline.json,
/// avança o produto para Writing e enfileira um job por capítulo.
/// Re-entrante: pula a IA quando o outline já existe e só avança o estágio uma vez.
/// </summary>
public sealed class OutlineJobHandler(
    IProductRepository products,
    INicheRepository niches,
    IKnowledgeService knowledge,
    IAiGateway aiGateway,
    IFileStore fileStore,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    ILogger<OutlineJobHandler> logger) : IJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.Outline;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<OutlineJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(ContentErrors.ProductNotFound(payload.ProductId));
        }

        var niche = await niches.GetByIdAsync(product.NicheId, ct);
        if (niche is null)
        {
            return Result.Failure(ContentErrors.NicheNotFound(product.NicheId));
        }

        var pack = await knowledge.EnsurePackAsync(niche, product.QualityTier, ct);
        if (pack.IsFailure)
        {
            return Result.Failure(pack.Error);
        }

        var outlinePath = ContentPaths.Outline(product.Slug);
        if (!fileStore.Exists(outlinePath))
        {
            var ai = await aiGateway.CompleteAsync(new AiRequest(
                Purpose: "ebook.outline",
                PromptTemplate: "ebook/outline",
                Variables: new Dictionary<string, string>
                {
                    ["niche"] = niche.Name,
                    ["knowledgePack"] = pack.Value,
                    ["tier"] = product.QualityTier.ToString(),
                    ["language"] = "pt-BR"
                },
                Tier: product.QualityTier,
                MaxOutputTokensEst: 1500,
                ProductId: product.Id), ct);

            if (ai.IsFailure)
            {
                return Result.Failure(ai.Error);
            }

            var validation = AiJsonValidate(ai.Value.Content);
            if (validation.IsFailure)
            {
                return Result.Failure(validation.Error);
            }

            await fileStore.WriteTextAsync(outlinePath, ai.Value.Content, ct);
        }

        var outline = await ContentPaths.ReadOutlineAsync(fileStore, product.Slug, ct);
        if (outline.IsFailure)
        {
            return Result.Failure(outline.Error);
        }

        product.SetTitle(outline.Value.Title);
        if (product.Stage == ProductStage.Outline)
        {
            var advanced = product.AdvanceStage(); // Outline → Writing
            if (advanced.IsFailure)
            {
                return Result.Failure(advanced.Error);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        foreach (var chapter in outline.Value.Chapters)
        {
            await jobQueue.EnqueueAsync(new JobRequest(
                ContentJobs.Chapter,
                JsonSerializer.Serialize(new ChapterJobPayload(product.Id, chapter.N), JsonOptions),
                ContentJobs.ChapterKey(product.Id, chapter.N),
                ProductId: product.Id), ct);
        }

        logger.LogInformation("Outline pronto para {Slug}: {Chapters} capítulos enfileirados",
            product.Slug, outline.Value.Chapters.Count);
        return Result.Success();
    }

    private static Result AiJsonValidate(string content)
    {
        var parsed = Ebook.Application.Common.Text.AiJson.Parse<OutlineDto>(content, "ebook.outline");
        return parsed.IsFailure ? Result.Failure(parsed.Error) : Result.Success();
    }
}
