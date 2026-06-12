using Ebook.Application.Administration.Auth;
using Ebook.Application.Administration.Dashboard;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Settings;
using Ebook.Application.DevTools;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Api.Endpoints;

public sealed record LoginRequest(string Username, string Password);

public sealed record AiEchoRequest(string Text);

public sealed record SetSettingRequest(string ValueJson);

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
