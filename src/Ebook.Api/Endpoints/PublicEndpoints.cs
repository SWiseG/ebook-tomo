using Ebook.Application.Content;
using Ebook.Domain.Abstractions;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        app.MapGet("/go/{slug}", async (string slug, EbookDbContext db, CancellationToken ct) =>
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

            logger.LogInformation("Clique de checkout {Slug}", slug);

            // checkout ainda não publicado (gate de aprovação / pré-E07): página de espera honesta
            return string.IsNullOrWhiteSpace(product.CheckoutUrl)
                ? Results.Content(ComingSoonHtml, "text/html; charset=utf-8")
                : Results.Redirect(product.CheckoutUrl);
        })
        .AllowAnonymous()
        .ExcludeFromDescription();

        app.MapGet("/px.gif", (string? s) =>
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                logger.LogInformation("Visita de LP {Slug}", s);
            }

            return Results.Bytes(TransparentGif, "image/gif");
        })
        .AllowAnonymous()
        .ExcludeFromDescription();
    }

    private const string ComingSoonHtml =
        "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">" +
        "<title>Em breve</title><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"></head>" +
        "<body style=\"font-family:sans-serif;display:grid;place-items:center;height:100vh;margin:0;text-align:center\">" +
        "<div><h1>Em breve</h1><p>Este produto estará disponível para compra em instantes.</p></div></body></html>";

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
