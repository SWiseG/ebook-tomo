using Ebook.Application.Analytics;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Application.Publishing;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Analytics;
using Ebook.Domain.Products;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ebook.Api.Endpoints;

/// <summary>
/// Rotas públicas (anônimas) da landing page (E06): serve o bundle HTML, redireciona o
/// clique de checkout (vínculo LP ↔ Kiwify resolvido em runtime) e o pixel de analytics.
/// Mapeadas antes do fallback do SPA para não colidirem com as rotas do Angular.
/// </summary>
public static class PublicEndpoints
{
    // GIF transparente 1×1 (GIF89a) servido pelo pixel de tracking
    private static readonly byte[] TransparentGif =
        Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    public static void MapPublicEndpoints(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LandingPage");

        app.MapGet("/lp/{slug}", async (
            string slug,
            [FromQuery(Name = "v")] string? variantTag,
            IArtifactStore artifacts,
            ILpVariantRepository lpVariants,
            IMetricsReader metricsReader,
            ISettingsStore settings,
            IRandom rng,
            CancellationToken ct) =>
        {
            if (!IsValidSlug(slug))
            {
                return Results.NotFound();
            }

            // Resolve qual variante servir
            var resolvedTag = await ResolveVariantTagAsync(
                slug, variantTag, lpVariants, metricsReader, settings, rng, ct);

            // Tenta servir a variante pedida/escolhida; se não existir, cai no bundle padrão
            if (resolvedTag is not null)
            {
                var variantPath = ContentPaths.LpVariant(slug, resolvedTag);
                var variantBytes = await artifacts.ReadBytesAsync(variantPath, ct);
                if (variantBytes is not null)
                {
                    return Results.Bytes(variantBytes, "text/html; charset=utf-8");
                }
            }

            var bytes = await artifacts.ReadBytesAsync(ContentPaths.LpBundle(slug), ct);
            return bytes is null
                ? Results.NotFound()
                : Results.Bytes(bytes, "text/html; charset=utf-8");
        })
        .AllowAnonymous()
        .ExcludeFromDescription();

        // mídia pública (cards/reels) para a Graph API do Meta consumir via image_url/video_url (E08/E10)
        app.MapGet("/media/{**path}", async (string path, IArtifactStore artifacts, CancellationToken ct) =>
        {
            var contentType = MediaContentType(path);
            if (contentType is null || !IsSafeMediaPath(path))
            {
                return Results.NotFound();
            }

            var bytes = await artifacts.ReadBytesAsync(path, ct);
            return bytes is null ? Results.NotFound() : Results.Bytes(bytes, contentType);
        })
        .AllowAnonymous()
        .ExcludeFromDescription();

        app.MapGet("/go/{slug}", async (
            string slug,
            [FromQuery(Name = "utm_source")] string? utmSource,
            [FromQuery(Name = "utm_campaign")] string? utmCampaign,
            [FromQuery(Name = "utm_content")] string? utmContent,
            EbookDbContext db,
            IAnalyticsRecorder recorder,
            CancellationToken ct) =>
        {
            if (!IsValidSlug(slug))
            {
                return Results.NotFound();
            }

            var product = await db.Products.AsNoTracking()
                .Where(p => p.Slug == slug)
                .Select(p => new { p.CheckoutUrl })
                .FirstOrDefaultAsync(ct);

            if (product is null)
            {
                return Results.NotFound();
            }

            await TryRecordAsync(recorder, new AnalyticsHit(
                slug, AnalyticsEventType.CheckoutClick, utmSource, utmCampaign, utmContent), logger, ct);

            // checkout ainda não publicado (gate de aprovação / pré-E07): página de espera honesta
            return string.IsNullOrWhiteSpace(product.CheckoutUrl)
                ? Results.Content(ComingSoonHtml, "text/html; charset=utf-8")
                : Results.Redirect(product.CheckoutUrl);
        })
        .AllowAnonymous()
        .ExcludeFromDescription();

        app.MapGet("/px.gif", async (
            string? s,
            [FromQuery(Name = "v")] string? variantTag,
            [FromQuery(Name = "utm_source")] string? utmSource,
            [FromQuery(Name = "utm_campaign")] string? utmCampaign,
            [FromQuery(Name = "utm_content")] string? utmContent,
            IAnalyticsRecorder recorder,
            CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(s) && IsValidSlug(s))
            {
                var safeTag = IsValidVariantTag(variantTag) ? variantTag : null;
                await TryRecordAsync(recorder, new AnalyticsHit(
                    s, AnalyticsEventType.Visit, utmSource, utmCampaign, utmContent, safeTag), logger, ct);
            }

            return Results.Bytes(TransparentGif, "image/gif");
        })
        .AllowAnonymous()
        .ExcludeFromDescription();

        // Webhook de vendas da Kiwify (E07-02): validado por token; grava SaleEvent idempotente.
        app.MapPost("/webhooks/kiwify", async (
            HttpRequest http,
            [FromQuery] string? token,
            IOptions<KiwifyOptions> kiwify,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var provided = token ?? http.Headers["X-Kiwify-Token"].ToString();
            if (!KiwifyWebhook.IsValidToken(provided, kiwify.Value.WebhookToken))
            {
                logger.LogWarning("Webhook Kiwify rejeitado: token inválido");
                return Results.Unauthorized();
            }

            using var reader = new StreamReader(http.Body);
            var body = await reader.ReadToEndAsync(ct);

            var mapped = KiwifyWebhookMapper.Map(body);
            if (mapped.IsFailure)
            {
                logger.LogWarning("Webhook Kiwify inválido: {Error}", mapped.Error.Code);
                return Results.BadRequest(new { error = mapped.Error.Code });
            }

            var command = mapped.Value;
            if (command is null)
            {
                // evento reconhecido mas não-gravável (pendente/recusado): confirma sem registrar venda
                logger.LogInformation("Webhook Kiwify ignorado: evento não-gravável");
                return Results.Ok();
            }

            var result = await dispatcher.SendAsync(command, ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Error.Message);
        })
        .AllowAnonymous()
        .ExcludeFromDescription();
    }

    private const string ComingSoonHtml =
        "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">" +
        "<title>Em breve</title><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"></head>" +
        "<body style=\"font-family:sans-serif;display:grid;place-items:center;height:100vh;margin:0;text-align:center\">" +
        "<div><h1>Em breve</h1><p>Este produto estará disponível para compra em instantes.</p></div></body></html>";

    // gravação de analytics nunca pode quebrar o pixel/redirect
    private static async Task TryRecordAsync(
        IAnalyticsRecorder recorder, AnalyticsHit hit, ILogger logger, CancellationToken ct)
    {
        try
        {
            await recorder.RecordAsync(hit, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao registrar analytics ({Type}) para {Slug}", hit.Type, hit.Slug);
        }
    }

    // só serve imagens/vídeos sob products/{slug}/{images|video}/ — bloqueia traversal e outros artefatos
    private static bool IsSafeMediaPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path.Split('/');
        return segments.Length == 4
            && segments[0] == "products"
            && IsValidSlug(segments[1])
            && segments[2] is "images" or "video";
    }

    private static string? MediaContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".mp4" => "video/mp4",
            _ => null
        };
    }

    private static bool IsValidSlug(string slug)
    {
        if (string.IsNullOrEmpty(slug) || slug.Length > 120)
        {
            return false;
        }

        foreach (var c in slug)
        {
            if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidVariantTag(string? tag) =>
        !string.IsNullOrEmpty(tag) && tag.Length <= 20 &&
        tag.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    private static async Task<string?> ResolveVariantTagAsync(
        string slug,
        string? requestedTag,
        ILpVariantRepository lpVariants,
        IMetricsReader metricsReader,
        ISettingsStore settings,
        IRandom rng,
        CancellationToken ct)
    {
        // Explicit tag via ?v= — honour directly (served by the file existence check upstream)
        if (IsValidVariantTag(requestedTag))
            return requestedTag;

        var variants = await lpVariants.GetBySlugAsync(slug, ct);
        if (variants.Count <= 1) return null;

        var smartTraffic = await settings.GetOrDefaultAsync(SettingKeys.LpSmartTraffic, false, ct);
        if (!smartTraffic)
        {
            // Stateless round-robin: distribute by TickCount to avoid any persistent state
            return variants[Math.Abs(Environment.TickCount) % variants.Count].VariantTag;
        }

        // Thompson Sampling: read real conversion stats from AnalyticsEvent
        var productId = variants[0].ProductId;
        var raw = await metricsReader.GetVariantStatsAsync(productId, days: 30, ct);
        var allStats = variants
            .Select(v => raw.FirstOrDefault(s => s.VariantTag == v.VariantTag)
                         ?? new VariantStats(v.VariantTag, 0, 0))
            .ToList();

        return LpVariantRouter.Route(allStats, rng);
    }
}
