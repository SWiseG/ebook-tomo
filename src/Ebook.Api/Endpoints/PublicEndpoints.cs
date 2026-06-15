using Ebook.Application.Analytics;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Content;
using Ebook.Application.Publishing;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Analytics;
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

        app.MapGet("/lp/{slug}", async (string slug, IArtifactStore artifacts, CancellationToken ct) =>
        {
            if (!IsValidSlug(slug))
            {
                return Results.NotFound();
            }

            var bytes = await artifacts.ReadBytesAsync(ContentPaths.LpBundle(slug), ct);
            return bytes is null
                ? Results.NotFound()
                : Results.Bytes(bytes, "text/html; charset=utf-8");
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
            [FromQuery(Name = "utm_source")] string? utmSource,
            [FromQuery(Name = "utm_campaign")] string? utmCampaign,
            [FromQuery(Name = "utm_content")] string? utmContent,
            IAnalyticsRecorder recorder,
            CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(s) && IsValidSlug(s))
            {
                await TryRecordAsync(recorder, new AnalyticsHit(
                    s, AnalyticsEventType.Visit, utmSource, utmCampaign, utmContent), logger, ct);
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

            var result = await dispatcher.SendAsync(mapped.Value, ct);
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
}
