using System.Globalization;
using System.Text;
using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Text;
using Ebook.Application.Knowledge;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content;

/// <summary>
/// Etapa Review: monta o manuscrito (capa + capítulos + moldura editorial),
/// faz a passada de revisão conforme o tier, gera a copy de venda + preço e
/// registra o artefato Manuscript. Avança o produto para Pdf (insumo da Fase E05).
/// </summary>
public sealed class ReviewJobHandler(
    IProductRepository products,
    INicheRepository niches,
    IArtifactRepository artifacts,
    IKnowledgeService knowledge,
    IAiGateway aiGateway,
    IFileStore fileStore,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<ReviewJobHandler> logger) : IJobHandler
{
    private const int ManuscriptVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.Review;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ReviewJobPayload>(payloadJson, JsonOptions)!;
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

        var manuscriptResult = await EnsureManuscriptAsync(product, outline.Value, ct);
        if (manuscriptResult.IsFailure)
        {
            return Result.Failure(manuscriptResult.Error);
        }

        var continuityResult = await EnsureContinuityAsync(product, outline.Value, ct);
        if (continuityResult.IsFailure)
        {
            return Result.Failure(continuityResult.Error);
        }

        var salesCopyResult = await EnsureSalesCopyAsync(product, outline.Value, ct);
        if (salesCopyResult.IsFailure)
        {
            return Result.Failure(salesCopyResult.Error);
        }

        var enteredPdf = false;
        if (product.Stage == ProductStage.Review)
        {
            var advanced = product.AdvanceStage(); // Review → Pdf (pronto para artes + renderização)
            if (advanced.IsFailure)
            {
                return Result.Failure(advanced.Error);
            }

            enteredPdf = true;
        }

        await unitOfWork.SaveChangesAsync(ct);

        if (enteredPdf)
        {
            // capa (E09) primeiro; o job de capa encadeia a renderização do PDF que a embute
            await jobQueue.EnqueueAsync(new JobRequest(
                ContentJobs.Cover,
                JsonSerializer.Serialize(new CoverJobPayload(product.Id), JsonOptions),
                ContentJobs.CoverKey(product.Id),
                ProductId: product.Id), ct);
        }

        logger.LogInformation("Manuscrito e copy de venda prontos para {Slug}", product.Slug);
        return Result.Success();
    }

    private async Task<Result> EnsureManuscriptAsync(Product product, OutlineDto outline, CancellationToken ct)
    {
        var path = ContentPaths.Manuscript(product.Slug, ManuscriptVersion);
        if (fileStore.Exists(path))
        {
            return Result.Success();
        }

        var review = await ReviewFramingAsync(product, outline, ct);
        if (review.IsFailure)
        {
            return Result.Failure(review.Error);
        }

        var chapters = new List<string>(outline.Chapters.Count);
        foreach (var chapter in outline.Chapters.OrderBy(c => c.N))
        {
            var body = await fileStore.ReadTextAsync(ContentPaths.Chapter(product.Slug, chapter.N), ct);
            if (body is null)
            {
                return Result.Failure(ContentErrors.ChapterNotInOutline(chapter.N));
            }

            chapters.Add($"## Capítulo {chapter.N} — {chapter.Title}\n\n{body.Trim()}");
        }

        var manuscript = Compose(outline, review.Value, chapters);
        var stored = await fileStore.WriteTextAsync(path, manuscript, ct);

        if (await artifacts.GetLatestAsync(product.Id, ArtifactType.Manuscript, ct) is null)
        {
            artifacts.Add(Artifact.Create(
                product.Id, ArtifactType.Manuscript, stored.RelativePath, stored.Sha256,
                ManuscriptVersion, "{}", clock.UtcNow));
        }

        return Result.Success();
    }

    /// <summary>Tiers Commercial/Premium recebem introdução/conclusão da IA; Draft usa moldura templada.</summary>
    private async Task<Result<ReviewDto>> ReviewFramingAsync(Product product, OutlineDto outline, CancellationToken ct)
    {
        if (product.QualityTier == QualityTier.Draft)
        {
            return Result.Success(new ReviewDto(
                Introduction: outline.Promise,
                Conclusion: "Coloque em prática o que aprendeu aqui — um passo de cada vez."));
        }

        var chapterList = string.Join("\n", outline.Chapters.Select(c => $"- {c.Title}"));
        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "ebook.review",
            PromptTemplate: "ebook/review",
            Variables: new Dictionary<string, string>
            {
                ["title"] = outline.Title,
                ["promise"] = outline.Promise,
                ["tone"] = outline.Tone,
                ["chapters"] = chapterList,
                ["tier"] = product.QualityTier.ToString(),
                ["language"] = "pt-BR"
            },
            Tier: product.QualityTier,
            MaxOutputTokensEst: 800,
            ProductId: product.Id), ct);

        return ai.IsFailure
            ? Result.Failure<ReviewDto>(ai.Error)
            : AiJson.Parse<ReviewDto>(ai.Value.Content, "ebook.review");
    }

    private async Task<Result> EnsureContinuityAsync(Product product, OutlineDto outline, CancellationToken ct)
    {
        if (product.QualityTier == QualityTier.Draft) return Result.Success();

        var markerPath = ContentPaths.ContinuityMarker(product.Slug);
        if (fileStore.Exists(markerPath)) return Result.Success();

        var manuscriptPath = ContentPaths.Manuscript(product.Slug, ManuscriptVersion);
        var manuscript = await fileStore.ReadTextAsync(manuscriptPath, ct);
        if (manuscript is null) return Result.Failure(ContentErrors.ManuscriptMissing(product.Slug));

        var outlineJson = await fileStore.ReadTextAsync(ContentPaths.Outline(product.Slug), ct) ?? "{}";

        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "ebook.continuity",
            PromptTemplate: "ebook/continuity",
            Variables: new Dictionary<string, string>
            {
                ["outline"] = outlineJson,
                ["manuscript"] = manuscript
            },
            Tier: product.QualityTier,
            MaxOutputTokensEst: 1200,
            ProductId: product.Id), ct);

        if (ai.IsFailure) return Result.Failure(ai.Error);

        var continuity = AiJson.Parse<ContinuityDto>(ai.Value.Content, "ebook.continuity");
        if (continuity.IsFailure) return Result.Failure(continuity.Error);

        var patched = ApplyContinuityPatches(manuscript, continuity.Value);
        await fileStore.WriteTextAsync(manuscriptPath, patched, ct);
        await fileStore.WriteTextAsync(markerPath, clock.UtcNow.ToString("O"), ct);

        logger.LogInformation(
            "Continuidade aplicada em {Slug}: {Bridges} pontes, {Removals} remoções, {Hooks} hooks ajustados",
            product.Slug,
            continuity.Value.Bridges.Count,
            continuity.Value.Removals.Count,
            continuity.Value.HookFixes.Count);

        return Result.Success();
    }

    private static string ApplyContinuityPatches(string manuscript, ContinuityDto continuity)
    {
        var ms = manuscript;

        // HookFixes: prepend substituto antes do primeiro parágrafo do capítulo.
        // Processado em ordem decrescente para não invalidar posições de capítulos anteriores.
        foreach (var fix in continuity.HookFixes.OrderByDescending(f => f.ChapterN))
        {
            var heading = $"## Capítulo {fix.ChapterN} — ";
            var headingIdx = ms.IndexOf(heading, StringComparison.Ordinal);
            if (headingIdx < 0) continue;

            var afterNewline = ms.IndexOf('\n', headingIdx);
            if (afterNewline < 0) continue;
            afterNewline++;

            while (afterNewline < ms.Length && ms[afterNewline] == '\n') afterNewline++;
            ms = ms[..afterNewline] + fix.Text.Trim() + "\n\n" + ms[afterNewline..];
        }

        // Bridges: inseridas ao fim do conteúdo do capítulo, antes do próximo heading.
        // Processado em ordem decrescente pelo mesmo motivo.
        foreach (var bridge in continuity.Bridges.OrderByDescending(b => b.ChapterN))
        {
            var heading = $"## Capítulo {bridge.ChapterN} — ";
            var headingIdx = ms.IndexOf(heading, StringComparison.Ordinal);
            if (headingIdx < 0) continue;

            var afterHeadingLine = ms.IndexOf('\n', headingIdx);
            if (afterHeadingLine < 0) continue;

            var nextSection = ms.IndexOf("\n## ", afterHeadingLine, StringComparison.Ordinal);
            var insertAt = nextSection >= 0 ? nextSection : ms.Length;

            var insertAdj = insertAt;
            while (insertAdj > 0 && ms[insertAdj - 1] == '\n') insertAdj--;

            ms = ms[..insertAdj] + "\n\n" + bridge.Text.Trim() + ms[insertAt..];
        }

        // Removals: correspondência exata, primeira ocorrência.
        foreach (var removal in continuity.Removals)
        {
            if (string.IsNullOrWhiteSpace(removal.Text)) continue;
            var idx = ms.IndexOf(removal.Text, StringComparison.Ordinal);
            if (idx >= 0) ms = ms[..idx] + ms[(idx + removal.Text.Length)..];
        }

        return ms;
    }

    private async Task<Result> EnsureSalesCopyAsync(Product product, OutlineDto outline, CancellationToken ct)
    {
        var path = ContentPaths.SalesCopy(product.Slug);
        var existing = await fileStore.ReadTextAsync(path, ct);
        if (existing is not null)
        {
            ApplySalesCopy(product, existing);
            return Result.Success();
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

        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "ebook.sales-copy",
            PromptTemplate: "ebook/sales-copy",
            Variables: new Dictionary<string, string>
            {
                ["title"] = outline.Title,
                ["promise"] = outline.Promise,
                ["tone"] = outline.Tone,
                ["knowledgePack"] = pack.Value,
                ["tier"] = product.QualityTier.ToString(),
                ["language"] = "pt-BR"
            },
            Tier: product.QualityTier,
            MaxOutputTokensEst: 1800,
            ProductId: product.Id), ct);

        if (ai.IsFailure)
        {
            return Result.Failure(ai.Error);
        }

        var parsed = AiJson.Parse<SalesCopyDto>(ai.Value.Content, "ebook.sales-copy");
        if (parsed.IsFailure)
        {
            return Result.Failure(parsed.Error);
        }

        await fileStore.WriteTextAsync(path, ai.Value.Content, ct);
        ApplySalesCopy(product, ai.Value.Content);
        return Result.Success();
    }

    private static void ApplySalesCopy(Product product, string salesCopyJson)
    {
        product.SetSalesCopy(salesCopyJson);
        var parsed = AiJson.Parse<SalesCopyDto>(salesCopyJson, "ebook.sales-copy");
        if (parsed.IsSuccess && parsed.Value.Price is { Current: > 0 } price)
        {
            product.SetPricing(price.Current, "BRL");
        }
    }

    private static string Compose(OutlineDto outline, ReviewDto framing, IReadOnlyList<string> chapters)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"# {outline.Title}\n");
        if (!string.IsNullOrWhiteSpace(outline.Subtitle))
        {
            sb.Append(CultureInfo.InvariantCulture, $"## {outline.Subtitle}\n");
        }

        sb.Append('\n').Append(framing.Introduction.Trim()).Append("\n\n");
        sb.AppendJoin("\n\n", chapters);
        sb.Append("\n\n").Append(framing.Conclusion.Trim()).Append('\n');
        return sb.ToString();
    }
}
