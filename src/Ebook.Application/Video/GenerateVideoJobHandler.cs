using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Text;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Ebook.Domain.Social;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Video;

/// <summary>
/// Gera um Reel (E10): roteiro via IA → card 9:16 por cena (E09) → narração (Piper) →
/// MP4 (FFmpeg) → Artifact(Video) + SocialPost(Reel) na agenda. Re-entrante: pula se o vídeo já existe.
/// </summary>
public sealed class GenerateVideoJobHandler(
    IProductRepository products,
    INicheRepository niches,
    ISocialPostRepository posts,
    IArtifactRepository artifacts,
    IAiGateway aiGateway,
    IImageComposer imageComposer,
    ITtsEngine tts,
    IVideoComposer videoComposer,
    IFileStore fileStore,
    IArtifactStore artifactStore,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<GenerateVideoJobHandler> logger) : IJobHandler
{
    private const int Version = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => VideoJobs.Generate;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<VideoJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(VideoErrors.ProductNotFound(payload.ProductId));
        }

        var videoPath = ContentPaths.VideoReel(product.Slug, Version);
        if (artifactStore.Exists(videoPath))
        {
            return Result.Success(); // já gerado
        }

        var niche = await niches.GetByIdAsync(product.NicheId, ct);
        var palette = PaletteCatalog.ForNiche(niche?.Slug ?? product.Slug);

        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "video.script",
            PromptTemplate: "video/script",
            Variables: new Dictionary<string, string>
            {
                ["productTitle"] = product.Title,
                ["niche"] = niche?.Name ?? product.Title,
                ["headline"] = Headline(product),
                ["language"] = "pt-BR"
            },
            MaxOutputTokensEst: 1200,
            ProductId: product.Id), ct);

        if (ai.IsFailure)
        {
            return Result.Failure(ai.Error);
        }

        var script = AiJson.Parse<VideoScriptDto>(ai.Value.Content, "video.script");
        if (script.IsFailure)
        {
            return Result.Failure(script.Error);
        }

        if (script.Value.Scenes.Count == 0)
        {
            return Result.Failure(AiErrors.InvalidOutput("roteiro sem cenas"));
        }

        await fileStore.WriteTextAsync(ContentPaths.VideoScript(product.Slug), ai.Value.Content, ct);

        var scenes = script.Value.Scenes
            .Select(s => new VideoScene(
                imageComposer.RenderSocial(new SocialArt(s.OnScreen, product.Title, ImageTemplate.Story, palette)),
                s.Seconds <= 0 ? 8 : s.Seconds))
            .ToList();

        var narration = string.Join(' ', script.Value.Scenes.Select(s => s.Narration));
        var audio = await tts.SynthesizeAsync(narration, ct);
        if (audio.IsFailure)
        {
            return Result.Failure(audio.Error);
        }

        var mp4 = await videoComposer.RenderAsync(new VideoSpec(scenes, audio.Value), ct);
        if (mp4.IsFailure)
        {
            return Result.Failure(mp4.Error);
        }

        var stored = await artifactStore.WriteBytesAsync(videoPath, mp4.Value, ct);
        if (await artifacts.GetLatestAsync(product.Id, ArtifactType.Video, ct) is null)
        {
            var meta = JsonSerializer.Serialize(new { scenes = scenes.Count, bytes = stored.SizeBytes }, JsonOptions);
            artifacts.Add(Artifact.Create(
                product.Id, ArtifactType.Video, stored.RelativePath, stored.Sha256, Version, meta, clock.UtcNow));
        }

        AddReelPost(product, script.Value, stored.RelativePath);

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Reel gerado para {Slug} ({Scenes} cenas, {Bytes} bytes)",
            product.Slug, scenes.Count, stored.SizeBytes);
        return Result.Success();
    }

    private void AddReelPost(Product product, VideoScriptDto script, string mediaPath)
    {
        var hashtags = script.Hashtags is null ? string.Empty : string.Join(' ', script.Hashtags);
        var utm = $"utm_source=instagram&utm_medium=social&utm_campaign={product.Slug}&utm_content=reel";
        var post = SocialPost.Plan(
            product.Id, SocialNetwork.Instagram, SocialPostType.Reel, 0,
            script.Caption, hashtags, ContentPaths.VideoScript(product.Slug), utm,
            clock.UtcNow.Date.AddDays(1).AddHours(22)); // ~19h BRT do dia seguinte
        post.SetMedia(mediaPath);
        posts.Add(post);
    }

    private static string Headline(Product product)
    {
        try
        {
            using var doc = JsonDocument.Parse(product.SalesCopyJson);
            if (doc.RootElement.TryGetProperty("headline", out var h) && h.ValueKind == JsonValueKind.String)
            {
                return h.GetString() ?? product.Title;
            }
        }
        catch (JsonException)
        {
            // sem copy → título
        }

        return product.Title;
    }
}
