using System.Globalization;
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

namespace Ebook.Application.Social;

/// <summary>
/// Gera o calendário de conteúdo de 30 dias (E08-02) via AI Gateway e renderiza os cards
/// (E09 cards via SkiaSharp). Cria um SocialPost por item (Planned, com UTM e agendamento).
/// Re-entrante: pula tudo quando o produto já tem posts.
/// </summary>
public sealed class GenerateCalendarJobHandler(
    IProductRepository products,
    INicheRepository niches,
    ISocialPostRepository posts,
    IAiGateway aiGateway,
    IImageComposer composer,
    IPhotoProvider photos,
    IFileStore fileStore,
    IArtifactStore artifactStore,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<GenerateCalendarJobHandler> logger) : IJobHandler
{
    private const int MaxPosts = 30;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => SocialJobs.Calendar;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<CalendarJobPayload>(payloadJson, JsonOptions)!;
        var product = await products.GetByIdAsync(payload.ProductId, ct);
        if (product is null)
        {
            return Result.Failure(SocialErrorsApp.ProductNotFound(payload.ProductId));
        }

        if (await posts.ExistsForProductAsync(product.Id, ct))
        {
            return Result.Success(); // calendário já gerado
        }

        var niche = await niches.GetByIdAsync(product.NicheId, ct);
        var palette = PaletteCatalog.ForNiche(niche?.Slug ?? product.Slug);

        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "social.calendar",
            PromptTemplate: "social/calendar",
            Variables: new Dictionary<string, string>
            {
                ["productTitle"] = product.Title,
                ["niche"] = niche?.Name ?? product.Title,
                ["headline"] = Headline(product),
                ["language"] = "pt-BR"
            },
            MaxOutputTokensEst: 1800,
            ProductId: product.Id), ct);

        if (ai.IsFailure)
        {
            return Result.Failure(ai.Error);
        }

        var plan = AiJson.Parse<CalendarPlanDto>(ai.Value.Content, "social.calendar");
        if (plan.IsFailure)
        {
            return Result.Failure(plan.Error);
        }

        var startUtc = clock.UtcNow;
        await WriteCalendarFileAsync(product.Slug, startUtc, plan.Value, ct);

        // AI-first: fundo gerado pela cadeia de mídia (Pollinations & cia.); Skia só sobrepõe texto.
        // Buscado uma vez e reaproveitado (cache content-addressable) por todos os cards do calendário.
        var background = await photos.TryGetBackgroundAsync(niche?.Name ?? product.Title, ct);

        var created = 0;
        foreach (var item in plan.Value.Posts.Take(MaxPosts))
        {
            var network = MapNetwork(item.Network);
            var postType = MapPostType(item.PostType);
            var scheduledAt = startUtc.Date.AddDays(Math.Max(0, item.Day - 1)).AddHours(ParseHourUtc(item.TimeSlot));
            var hashtags = item.Hashtags is null ? string.Empty : string.Join(' ', item.Hashtags);
            var utm = BuildUtm(product.Slug, network, postType);

            var post = SocialPost.Plan(
                product.Id, network, postType, item.Day, item.Copy, hashtags,
                ContentPaths.SocialCalendar(product.Slug), utm, scheduledAt);

            var slides = item.Slides?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? [];
            if (slides.Count >= 2)
            {
                // carrossel (E09): capa com headline + 1 slide por texto; o primeiro caminho é a capa.
                var images = composer.RenderCarousel(new CarouselArt(item.Headline, product.Title, slides, palette), background);
                var paths = new List<string>(images.Count);
                for (var i = 0; i < images.Count; i++)
                {
                    var path = i == 0
                        ? ContentPaths.SocialCard(product.Slug, item.Day)
                        : ContentPaths.SocialSlide(product.Slug, item.Day, i);
                    var stored = await artifactStore.WriteBytesAsync(path, images[i], ct);
                    paths.Add(stored.RelativePath);
                }

                post.SetMedia(paths[0]);
                post.SetCarousel(string.Join(',', paths));
            }
            else
            {
                var card = composer.RenderSocial(
                    new SocialArt(item.Headline, product.Title, ImageTemplate.SocialCard, palette), background);
                var stored = await artifactStore.WriteBytesAsync(
                    ContentPaths.SocialCard(product.Slug, item.Day), card, ct);
                post.SetMedia(stored.RelativePath);
            }

            posts.Add(post);
            created++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Calendário social gerado para {Slug}: {Count} posts", product.Slug, created);
        return Result.Success();
    }

    private async Task WriteCalendarFileAsync(string slug, DateTime startUtc, CalendarPlanDto plan, CancellationToken ct)
    {
        var calendar = new { productSlug = slug, startDate = startUtc, days = 30, posts = plan.Posts };
        await fileStore.WriteTextAsync(
            ContentPaths.SocialCalendar(slug), JsonSerializer.Serialize(calendar, JsonOptions), ct);
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
            // copy ausente → título
        }

        return product.Title;
    }

    private static SocialNetwork MapNetwork(string value) => value.ToLowerInvariant() switch
    {
        "facebook" or "fb" => SocialNetwork.Facebook,
        "x" or "twitter" => SocialNetwork.X,
        _ => SocialNetwork.Instagram
    };

    private static SocialPostType MapPostType(string value) => value.ToLowerInvariant() switch
    {
        "launch" or "lancamento" => SocialPostType.Launch,
        "proof" or "prova" => SocialPostType.Proof,
        "offer" or "oferta" => SocialPostType.Offer,
        "reel" => SocialPostType.Reel,
        _ => SocialPostType.Value
    };

    // hora informada em BRT (UTC-3) → UTC; default 13h UTC (~10h BRT)
    private static int ParseHourUtc(string? timeSlot)
    {
        if (!string.IsNullOrWhiteSpace(timeSlot)
            && int.TryParse(timeSlot.Split(':')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
            && hour is >= 0 and <= 23)
        {
            return (hour + 3) % 24;
        }

        return 13;
    }

    private static string BuildUtm(string slug, SocialNetwork network, SocialPostType postType) =>
        $"utm_source={network.ToString().ToLowerInvariant()}&utm_medium=social" +
        $"&utm_campaign={slug}&utm_content={postType.ToString().ToLowerInvariant()}";
}
