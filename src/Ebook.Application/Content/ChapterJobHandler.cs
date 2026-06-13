using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content;

/// <summary>
/// Etapa Writing: gera um capítulo por job (retomável). Contexto mínimo —
/// outline + especificação do capítulo + resumo de 1 parágrafo do capítulo anterior.
/// Quando todos os capítulos existem, avança o produto para Review e enfileira a revisão.
/// </summary>
public sealed class ChapterJobHandler(
    IProductRepository products,
    IAiGateway aiGateway,
    IFileStore fileStore,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    ILogger<ChapterJobHandler> logger) : IJobHandler
{
    private const int PreviousSummaryChars = 600;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.Chapter;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ChapterJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(ContentErrors.ProductNotFound(payload.ProductId));
        }

        var outline = await ContentPaths.ReadOutlineAsync(fileStore, product.Slug, ct);
        if (outline.IsFailure)
        {
            return Result.Failure(outline.Error);
        }

        var spec = outline.Value.Chapters.FirstOrDefault(c => c.N == payload.ChapterNumber);
        if (spec is null)
        {
            return Result.Failure(ContentErrors.ChapterNotInOutline(payload.ChapterNumber));
        }

        var chapterPath = ContentPaths.Chapter(product.Slug, spec.N);
        if (!fileStore.Exists(chapterPath))
        {
            var previousSummary = await PreviousSummaryAsync(product.Slug, spec.N, ct);
            var ai = await aiGateway.CompleteAsync(new AiRequest(
                Purpose: "ebook.chapter",
                PromptTemplate: "ebook/chapter",
                Variables: new Dictionary<string, string>
                {
                    ["bookTitle"] = outline.Value.Title,
                    ["tone"] = outline.Value.Tone,
                    ["chapterNumber"] = spec.N.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["chapterTitle"] = spec.Title,
                    ["chapterGoal"] = spec.Goal,
                    ["keyPoints"] = string.Join("; ", spec.KeyPoints),
                    ["targetWords"] = spec.TargetWords.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["previousSummary"] = previousSummary,
                    ["language"] = "pt-BR"
                },
                Tier: product.QualityTier,
                MaxOutputTokensEst: spec.TargetWords + 300,
                ProductId: product.Id), ct);

            if (ai.IsFailure)
            {
                return Result.Failure(ai.Error);
            }

            await fileStore.WriteTextAsync(chapterPath, ai.Value.Content, ct);
            logger.LogInformation("Capítulo {N}/{Total} gerado para {Slug}",
                spec.N, outline.Value.Chapters.Count, product.Slug);
        }

        await MaybeFinishWritingAsync(product, outline.Value, ct);
        return Result.Success();
    }

    private async Task MaybeFinishWritingAsync(Product product, OutlineDto outline, CancellationToken ct)
    {
        var allWritten = outline.Chapters.All(c => fileStore.Exists(ContentPaths.Chapter(product.Slug, c.N)));
        if (!allWritten || product.Stage != ProductStage.Writing)
        {
            return;
        }

        var advanced = product.AdvanceStage(); // Writing → Review
        if (advanced.IsFailure)
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(ct);
        await jobQueue.EnqueueAsync(new JobRequest(
            ContentJobs.Review,
            JsonSerializer.Serialize(new ReviewJobPayload(product.Id), JsonOptions),
            ContentJobs.ReviewKey(product.Id),
            ProductId: product.Id), ct);

        logger.LogInformation("Todos os capítulos de {Slug} prontos — revisão enfileirada", product.Slug);
    }

    private async Task<string> PreviousSummaryAsync(string slug, int n, CancellationToken ct)
    {
        if (n <= 1)
        {
            return string.Empty;
        }

        var previous = await fileStore.ReadTextAsync(ContentPaths.Chapter(slug, n - 1), ct);
        if (string.IsNullOrWhiteSpace(previous))
        {
            return string.Empty;
        }

        var trimmed = previous.Trim();
        return trimmed.Length <= PreviousSummaryChars ? trimmed : trimmed[..PreviousSummaryChars];
    }
}
