using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Ebook.Application.Content.Pdf;
using Ebook.Application.Media;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content;

/// <summary>
/// Etapa Pdf: renderiza o manuscrito aprovado em um PDF comercial (tema por nicho),
/// registra o artefato Pdf, avança o produto para Lp e enfileira a geração da landing page (E06).
/// Re-entrante: pula a renderização quando o PDF já existe.
/// Frente D: injeta uma ilustração IA após cada capítulo (1 imagem/2-3 páginas), geradas em paralelo.
/// </summary>
public sealed class PdfJobHandler(
    IProductRepository products,
    INicheRepository niches,
    IArtifactRepository artifacts,
    IPdfRenderer renderer,
    IFileStore fileStore,
    IArtifactStore artifactStore,
    IJobQueue jobQueue,
    IMediaGateway mediaGateway,
    IImageComposer imageComposer,
    IAiGateway aiGateway,
    IPromptLibrary promptLibrary,
    IPaletteResolver paletteResolver,
    IBrandResolver brandResolver,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<PdfJobHandler> logger) : IJobHandler
{
    private const int PdfVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.Pdf;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<PdfJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(ContentErrors.ProductNotFound(payload.ProductId));
        }

        var pdfPath = ContentPaths.Pdf(product.Slug, PdfVersion);
        if (!artifactStore.Exists(pdfPath))
        {
            var rendered = await RenderAsync(product, pdfPath, ct);
            if (rendered.IsFailure)
            {
                return rendered;
            }
        }

        if (product.Stage == ProductStage.Pdf)
        {
            var advanced = product.AdvanceStage(); // Pdf → Lp
            if (advanced.IsFailure)
            {
                return Result.Failure(advanced.Error);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("PDF pronto para {Slug}", product.Slug);

        await jobQueue.EnqueueAsync(new JobRequest(
            ContentJobs.Lp,
            JsonSerializer.Serialize(new LpJobPayload(product.Id), JsonOptions),
            ContentJobs.LpKey(product.Id),
            ProductId: product.Id), ct);

        return Result.Success();
    }

    private async Task<Result> RenderAsync(Product product, string pdfPath, CancellationToken ct)
    {
        var manuscript = await fileStore.ReadTextAsync(ContentPaths.Manuscript(product.Slug, 1), ct);
        if (manuscript is null)
        {
            return Result.Failure(ContentErrors.ManuscriptMissing(product.Slug));
        }

        var outline = await ContentPaths.ReadOutlineAsync(fileStore, product.Slug, ct);
        var tagline = outline.IsSuccess ? outline.Value.Promise : null;

        var nicheSlug = await NicheSlugAsync(product, ct);
        var theme = PdfThemeSelector.ForNiche(nicheSlug);
        var palette = await paletteResolver.ResolveAsync(product.Slug, nicheSlug, ct);
        var cta = await BuildCtaAsync(product, ct);
        var book = PdfBookComposer.Build(manuscript, product.Title, tagline, cta, theme, palette);

        // Fase 4: Diretor de Arte por IA decide, por capítulo, foto vs ilustração + query/prompt concretos.
        var visualPlan = outline.IsSuccess
            ? await BuildVisualPlanAsync(outline.Value, nicheSlug, product.Id, ct)
            : EmptyPlan;

        // Frente D: ilustrações por capítulo (geradas em paralelo via Media Gateway), guiadas pelo plano.
        var brand = await brandResolver.ResolveAsync(product.Slug, nicheSlug, ct); // docs/15 Frente A
        var bodyWithImages = await InjectIllustrationsAsync(book.Body, nicheSlug, product.Id, visualPlan, brand, ct);
        book = book with { Body = bodyWithImages };

        // WS-E: infográficos de métricas compostos no Skia (blocos Infographic → imagens)
        book = book with { Body = ComposeInfographics(book.Body, palette) };

        var coverImage = await artifactStore.ReadBytesAsync(ContentPaths.Cover(product.Slug), ct);
        var bytes = renderer.Render(book, coverImage);
        var stored = await artifactStore.WriteBytesAsync(pdfPath, bytes, ct);

        if (await artifacts.GetLatestAsync(product.Id, ArtifactType.Pdf, ct) is null)
        {
            var meta = JsonSerializer.Serialize(new { theme = theme.ToString(), bytes = stored.SizeBytes }, JsonOptions);
            artifacts.Add(Artifact.Create(
                product.Id, ArtifactType.Pdf, stored.RelativePath, stored.Sha256, PdfVersion, meta, clock.UtcNow));
        }

        logger.LogInformation("PDF renderizado para {Slug} ({Bytes} bytes, tema {Theme})",
            product.Slug, stored.SizeBytes, theme);
        return Result.Success();
    }

    /// <summary>
    /// Gera uma ilustração para cada capítulo (H2) em paralelo e insere o bloco Image logo após o heading.
    /// Máximo 6 capítulos para não estourar cota diária de nenhum provedor em uma única geração.
    /// Capítulos sem imagem (provedor falhou/esgotou) simplesmente não recebem o bloco — sem quebrar o PDF.
    /// </summary>
    private async Task<IReadOnlyList<MarkdownBlock>> InjectIllustrationsAsync(
        IReadOnlyList<MarkdownBlock> body,
        string nicheSlug,
        Guid productId,
        IReadOnlyDictionary<string, VisualDirectiveDto> plan,
        ProductBrand brand,
        CancellationToken ct)
    {
        // docs/17 P1-4: 1 ilustração por capítulo (antes só 6); teto p/ conter custo em livros longos.
        var chapters = body
            .Where(b => b.Kind == MarkdownBlockKind.Heading && b.Level == 2)
            .Take(12)
            .ToList();

        if (chapters.Count == 0)
        {
            return body;
        }

        // gera todas as imagens em paralelo, cada capítulo guiado pela diretriz do Diretor de Arte (Fase 4)
        var imageTasks = chapters.ToDictionary(
            c => c.Text,
            c => GenerateIllustrationAsync(c.Text, nicheSlug, productId, plan.GetValueOrDefault(ChapterKey(c.Text)), brand, ct));

        await Task.WhenAll(imageTasks.Values);

        var imageByChapter = imageTasks.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Result);

        // intercala: cada H2 capítulo recebe seu bloco Image logo após (antes do primeiro parágrafo)
        var result = new List<MarkdownBlock>(body.Count + chapters.Count);
        foreach (var block in body)
        {
            result.Add(block);

            if (block.Kind == MarkdownBlockKind.Heading
                && block.Level == 2
                && imageByChapter.TryGetValue(block.Text, out var img)
                && img is not null)
            {
                result.Add(MarkdownBlock.Image(img));
            }
        }

        return result;
    }

    /// <summary>
    /// WS-E (docs/13): substitui cada bloco Infographic por uma imagem composta no Skia (banda de
    /// 2–3 métricas com as cores do nicho). Falha de composição apenas descarta o bloco — sem quebrar o PDF.
    /// </summary>
    private IReadOnlyList<MarkdownBlock> ComposeInfographics(IReadOnlyList<MarkdownBlock> body, NichePalette palette)
    {
        if (!body.Any(b => b.Kind == MarkdownBlockKind.Infographic))
        {
            return body;
        }

        var result = new List<MarkdownBlock>(body.Count);
        foreach (var block in body)
        {
            if (block.Kind != MarkdownBlockKind.Infographic)
            {
                result.Add(block);
                continue;
            }

            var metrics = block.Items.Select(ParseMetric).OfType<InfographicMetric>().ToList();
            if (metrics.Count == 0)
            {
                continue; // sem métricas válidas: descarta o bloco
            }

            try
            {
                var bytes = imageComposer.RenderInfographic(new InfographicArt(metrics, palette));
                result.Add(MarkdownBlock.Image(bytes));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao compor infográfico; ignorando o bloco");
            }
        }

        return result;
    }

    // "97% | descrição" → InfographicMetric; sem número → null.
    private static InfographicMetric? ParseMetric(string cell)
    {
        var idx = cell.IndexOf('|');
        if (idx <= 0)
        {
            return null;
        }

        var number = cell[..idx].Trim();
        var label = cell[(idx + 1)..].Trim();
        return number.Length > 0 ? new InfographicMetric(number, label) : null;
    }

    private async Task<byte[]?> GenerateIllustrationAsync(
        string chapterTitle, string nicheSlug, Guid productId, VisualDirectiveDto? directive, ProductBrand brand, CancellationToken ct)
    {
        try
        {
            var isPhoto = string.Equals(directive?.Mode, "photo", StringComparison.OrdinalIgnoreCase);
            var basePrompt = !string.IsNullOrWhiteSpace(directive?.Prompt)
                ? directive!.Prompt
                : await BuildIllustrationPromptAsync(chapterTitle, nicheSlug, ct);
            var prompt = brand.Decorate(basePrompt); // docs/15 Frente A: direção de arte única
            var query = !string.IsNullOrWhiteSpace(directive?.Query)
                ? directive!.Query
                : nicheSlug.Replace('-', ' '); // palavras-chave p/ bancos de foto

            var brief = new MediaBrief(
                Purpose: "chapter-illustration",
                Prompt: prompt,
                Query: query,
                NicheSlug: nicheSlug,
                Width: 1280,
                Height: 640,
                ProductId: productId,
                Kind: directive is null ? MediaKind.Auto : isPhoto ? MediaKind.Photo : MediaKind.Illustration);

            var result = await mediaGateway.GenerateAsync(brief, ct);
            // docs/17 P1-5: normaliza p/ banner 2:1 — evita ilustração retrato/quadrada minúscula no PDF.
            return result.IsSuccess ? imageComposer.FitBanner(result.Value.Bytes) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao gerar ilustração para capítulo '{Title}'; ignorando", chapterTitle);
            return null;
        }
    }

    private static readonly IReadOnlyDictionary<string, VisualDirectiveDto> EmptyPlan =
        new Dictionary<string, VisualDirectiveDto>();

    /// <summary>
    /// Fase 4 — Diretor de Arte por IA: a partir do outline, a IA decide por capítulo o tipo de imagem
    /// (foto vs ilustração) e a query/prompt concretos. Indexado por <see cref="ChapterKey"/>. Cacheado
    /// pelo AI Gateway; qualquer falha cai para os prompts genéricos (não quebra a geração).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, VisualDirectiveDto>> BuildVisualPlanAsync(
        OutlineDto outline, string nicheSlug, Guid productId, CancellationToken ct)
    {
        try
        {
            var chapters = string.Join("\n", outline.Chapters.Select(c =>
                $"- {c.Title} | objetivo: {c.Goal} | pontos: {string.Join(", ", c.KeyPoints)}"));

            var ai = await aiGateway.CompleteAsync(new AiRequest(
                Purpose: "ebook.visual-plan",
                PromptTemplate: "ebook/visual-plan",
                Variables: new Dictionary<string, string>
                {
                    ["niche"] = nicheSlug.Replace('-', ' '),
                    ["chapters"] = chapters,
                },
                ProductId: productId), ct);

            if (ai.IsFailure)
            {
                return EmptyPlan;
            }

            var parsed = AiJson.Parse<VisualPlanDto>(ai.Value.Content, "ebook.visual-plan");
            if (parsed.IsFailure || parsed.Value.Chapters is null)
            {
                return EmptyPlan;
            }

            return parsed.Value.Chapters
                .Where(d => !string.IsNullOrWhiteSpace(d.Title))
                .GroupBy(d => ChapterKey(d.Title))
                .ToDictionary(g => g.Key, g => g.First());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Plano visual da IA falhou; usando prompts genéricos");
            return EmptyPlan;
        }
    }

    // chave de capítulo: remove o prefixo "Capítulo N — " e normaliza, casando o título do plano com o H2.
    private static string ChapterKey(string text)
    {
        string[] seps = [" — ", " – ", " - ", ": "];
        foreach (var sep in seps)
        {
            var idx = text.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0 && text.StartsWith("Cap", StringComparison.OrdinalIgnoreCase))
            {
                text = text[(idx + sep.Length)..];
                break;
            }
        }

        return text.Trim().ToLowerInvariant();
    }

    private async Task<string> BuildIllustrationPromptAsync(string chapterTitle, string nicheSlug, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["chapterTitle"] = chapterTitle,
            ["nicheSlug"] = nicheSlug,
        };

        var rendered = await promptLibrary.RenderAsync("media/illustration", vars, ct);
        return rendered.IsSuccess
            ? rendered.Value
            : $"editorial illustration for '{chapterTitle}' about {nicheSlug}, no text, professional, minimalist";
    }

    private async Task<string> NicheSlugAsync(Product product, CancellationToken ct)
    {
        var niche = await niches.GetByIdAsync(product.NicheId, ct);
        return niche?.Slug ?? product.Slug;
    }

    private async Task<PdfCta> BuildCtaAsync(Product product, CancellationToken ct)
    {
        var headline = product.Title;
        var salesCopy = await fileStore.ReadTextAsync(ContentPaths.SalesCopy(product.Slug), ct);
        if (salesCopy is not null)
        {
            var parsed = AiJson.Parse<SalesCopyDto>(salesCopy, "ebook.sales-copy");
            if (parsed.IsSuccess && !string.IsNullOrWhiteSpace(parsed.Value.Headline))
            {
                headline = parsed.Value.Headline;
            }
        }

        return new PdfCta(headline, product.CheckoutUrl);
    }
}
