using Ebook.Application.Administration.Auth;
using Ebook.Application.Administration.Dashboard;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Application.DevTools;
using Ebook.Application.Discovery;
using Ebook.Application.Publishing;
using Ebook.Domain.Products;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Api.Endpoints;

public sealed record LoginRequest(string Username, string Password);

public sealed record AiEchoRequest(string Text);

public sealed record SetSettingRequest(string ValueJson);

public sealed record GenerateProductRequest(Guid NicheId, string? Title, QualityTier? Tier);

public sealed record RejectProductRequest(string? Reason);

public sealed record CompletePublishingRequest(string KiwifyProductId, string CheckoutUrl);

public static class Endpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapPost("/auth/login", async (LoginRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new LoginCommand(request.Username, request.Password), ct)).ToHttp())
            .AllowAnonymous()
            .WithTags("Auth")
            .WithSummary("Autentica o admin e emite JWT");

        var secured = api.MapGroup("").RequireAuthorization();

        secured.MapGet("/dashboard/summary", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetDashboardSummaryQuery(), ct)).ToHttp())
            .WithTags("Dashboard")
            .WithSummary("KPIs do painel: produtos, jobs e consumo de IA");

        secured.MapGet("/jobs", async (string? status, int page, int size, EbookDbContext db, CancellationToken ct) =>
        {
            var query = db.Jobs.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<JobStatus>(status, ignoreCase: true, out var parsed))
            {
                query = query.Where(j => j.Status == parsed);
            }

            var pageSize = size is < 1 or > 200 ? 50 : size;
            var pageNumber = page < 1 ? 1 : page;
            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(j => j.CreatedAtUtc)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Results.Ok(new { total, page = pageNumber, size = pageSize, items });
        })
        .WithTags("Jobs")
        .WithSummary("Lista jobs com filtro por status e paginação");

        secured.MapPost("/jobs/{id:guid}/retry", async (Guid id, EbookDbContext db, CancellationToken ct) =>
        {
            var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, ct);
            if (job is null)
            {
                return Results.NotFound();
            }

            if (job.Status != JobStatus.Dead)
            {
                return Results.Problem(title: "Job.NotDead",
                    detail: "Apenas jobs em dead-letter podem ser reenfileirados.", statusCode: 400);
            }

            job.Status = JobStatus.Pending;
            job.Attempts = 0;
            job.ScheduledAtUtc = DateTime.UtcNow;
            job.LastError = null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(job);
        })
        .WithTags("Jobs")
        .WithSummary("Reenfileira um job em dead-letter");

        secured.MapGet("/niches", async (string? status, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetNichesQuery(status), ct)).ToHttp())
            .WithTags("Niches")
            .WithSummary("Lista nichos descobertos (filtro opcional por status)");

        secured.MapPost("/niches/discover", async (int? topN, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new DiscoverNichesCommand(topN), ct)).ToHttp())
            .WithTags("Niches")
            .WithSummary("Dispara a descoberta de nichos manualmente (enfileira o job)");

        secured.MapPost("/niches/{id:guid}/approve", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new ApproveNicheCommand(id), ct)).ToHttp())
            .WithTags("Niches")
            .WithSummary("Aprova um nicho candidato (Selected)");

        secured.MapPost("/niches/{id:guid}/discard", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new DiscardNicheCommand(id), ct)).ToHttp())
            .WithTags("Niches")
            .WithSummary("Descarta um nicho");

        secured.MapPost("/products", async (GenerateProductRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(
                new GenerateProductCommand(request.NicheId, request.Title, request.Tier ?? QualityTier.Commercial), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Inicia o pipeline de geração de um produto a partir de um nicho");

        secured.MapGet("/products", async (string? status, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetProductsQuery(status), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Lista produtos (filtro opcional por status)");

        secured.MapGet("/products/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetProductDetailQuery(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Detalhe de um produto");

        secured.MapGet("/products/{id:guid}/outline", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetOutlineQuery(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Outline do manuscrito");

        secured.MapGet("/products/{id:guid}/manuscript", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.QueryAsync(new GetManuscriptQuery(id), ct);
            return result.IsSuccess
                ? Results.Text(result.Value, "text/markdown")
                : result.ToHttp();
        })
            .WithTags("Products")
            .WithSummary("Manuscrito montado (Markdown) para revisão no painel");

        secured.MapGet("/products/{id:guid}/pdf", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.QueryAsync(new GetProductPdfQuery(id), ct);
            return result.IsSuccess
                ? Results.File(result.Value, "application/pdf", $"ebook-{id}.pdf")
                : result.ToHttp();
        })
            .WithTags("Products")
            .WithSummary("Baixa o PDF gerado do produto");

        secured.MapGet("/products/{id:guid}/cover", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.QueryAsync(new GetProductCoverQuery(id), ct);
            return result.IsSuccess
                ? Results.File(result.Value, "image/png", $"cover-{id}.png")
                : result.ToHttp();
        })
            .WithTags("Products")
            .WithSummary("Baixa a capa gerada do produto");

        secured.MapPost("/products/{id:guid}/approve", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new ApproveProductCommand(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Aprova a publicação do produto (AwaitingApproval → Publishing)");

        secured.MapPost("/products/{id:guid}/reject", async (Guid id, RejectProductRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RejectProductCommand(id, request.Reason ?? string.Empty), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Rejeita o produto e o devolve para retrabalho");

        secured.MapPost("/products/{id:guid}/publish", async (Guid id, CompletePublishingRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(
                new CompletePublishingCommand(id, request.KiwifyProductId, request.CheckoutUrl), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Conclui a publicação manualmente (id Kiwify + URL de checkout → Live)");

        secured.MapGet("/settings", async (ISettingsStore settings, CancellationToken ct) =>
            Results.Ok(await settings.GetAllAsync(ct)))
            .WithTags("Settings")
            .WithSummary("Lista todas as configurações dinâmicas");

        secured.MapPut("/settings/{key}", async (string key, SetSettingRequest request, EbookDbContext db, CancellationToken ct) =>
        {
            // grava o JSON cru informado pelo painel (o ISettingsStore tiparia o valor)
            var record = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (record is null)
            {
                db.Settings.Add(new SettingRecord { Key = key, ValueJson = request.ValueJson, UpdatedAtUtc = DateTime.UtcNow });
            }
            else
            {
                record.ValueJson = request.ValueJson;
                record.UpdatedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithTags("Settings")
        .WithSummary("Grava/atualiza uma configuração (JSON cru)");

        // Critério de saída da Fase 0: valida AI Gateway ponta a ponta (cache → Claude CLI)
        secured.MapPost("/dev/ai-echo", async (AiEchoRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new AiEchoCommand(request.Text), ct)).ToHttp())
            .WithTags("Dev")
            .WithSummary("Smoke test do AI Gateway (cache → Claude CLI via assinatura Pro)");
    }
}
