using Ebook.Application.Administration.Auth;
using Ebook.Application.Administration.Dashboard;
using Ebook.Application.Administration.Media;
using Ebook.Application.Administration.Provenance;
using Ebook.Application.Administration.Sources;
using Ebook.Application.Analytics;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Application.Content.Lp.Lab;
using Ebook.Application.DevTools;
using Ebook.Application.Discovery;
using Ebook.Application.Optimization;
using Ebook.Application.Publishing;
using Ebook.Application.Social;
using Ebook.Domain.Abstractions;
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

public sealed record PublicationDataRequest(
    string Platform, string Title, string Description, decimal Price, string Currency,
    string EmailLanguage, string Category);

public sealed record CheckoutLinkRequest(string CheckoutUrl);

public sealed record MarkPublishedRequest(string Platform);

public sealed record CreateChannelRequest(Guid NicheId, string Name);

public sealed record ConnectChannelRequest(
    string? Name, string? PageId, string? IgUserId, string AccessToken,
    string? PublicMediaBaseUrl, DateTime? TokenExpiresAtUtc);

public sealed record SetApprovalRequest(bool Approved);

public sealed record EditPostRequest(string Caption, string Hashtags);

public sealed record GenerateTestLpRequest(Guid NicheId, string? Feedback);

public sealed record SaveLpMemoryRequest(Guid NicheId, string? Feedback);

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

        secured.MapGet("/media/telemetry", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetMediaTelemetryQuery(), ct)).ToHttp())
            .WithTags("Media")
            .WithSummary("Telemetria do Media Gateway: uso por provedor, cache e cotas (E14-08)");

        secured.MapGet("/sources/telemetry", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetSourcesTelemetryQuery(), ct)).ToHttp())
            .WithTags("Sources")
            .WithSummary("Telemetria unificada de fontes externas: texto (IA) + imagem (Media Gateway)");

        secured.MapGet("/logs", (
            Ebook.Api.Observability.InMemoryLogBuffer buffer,
            long afterId = 0,
            int limit = 200) =>
        {
            var entries = buffer.GetAfter(afterId, Math.Clamp(limit, 1, 500));
            return Results.Ok(new { lastId = buffer.LastId, entries });
        })
        .WithTags("Observability")
        .WithSummary("Stream de logs em memória (últimas 1000 entradas, polling com afterId)");

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

        secured.MapGet("/products/{id:guid}/kiwify-match", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new ResolveKiwifyProductQuery(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Busca, na API da Kiwify, o id + URL de checkout do produto (casamento por nome)");

        secured.MapPut("/products/{id:guid}/publication-data", async (Guid id, PublicationDataRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SetPublicationDataCommand(
                id, request.Platform, request.Title, request.Description, request.Price,
                request.Currency, request.EmailLanguage, request.Category), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Grava os dados de publicação (plataforma, nome, descrição, preço, etc.)");

        secured.MapPut("/products/{id:guid}/checkout", async (Guid id, CheckoutLinkRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SetCheckoutLinkCommand(id, request.CheckoutUrl), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Insere o link de checkout (absorvido pela landing page em runtime)");

        secured.MapPost("/products/{id:guid}/mark-published", async (Guid id, MarkPublishedRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new MarkPublishedCommand(id, request.Platform), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Marca como publicado (Publishing → Published) e dispara a sincronização");

        secured.MapPost("/products/{id:guid}/sync", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SyncProductCommand(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Sincroniza: confirma o produto na Kiwify (Synchronized/Unsynchronized)");

        secured.MapGet("/products/{id:guid}/social", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetProductSocialQuery(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Agenda de conteúdo social do produto (calendário de 30 dias)");

        // ── Distribuição: canais (1 por nicho) ──
        secured.MapGet("/channels", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetChannelsQuery(), ct)).ToHttp())
            .WithTags("Distribution")
            .WithSummary("Lista os canais sociais (por nicho)");

        secured.MapPost("/channels", async (CreateChannelRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new CreateChannelCommand(request.NicheId, request.Name), ct)).ToHttp())
            .WithTags("Distribution")
            .WithSummary("Cria um canal para um nicho");

        secured.MapPut("/channels/{id:guid}/connect", async (Guid id, ConnectChannelRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new ConnectChannelCommand(
                id, request.Name, request.PageId, request.IgUserId, request.AccessToken,
                request.PublicMediaBaseUrl, request.TokenExpiresAtUtc), ct)).ToHttp())
            .WithTags("Distribution")
            .WithSummary("Conecta/atualiza as credenciais do Meta no canal");

        // ── Distribuição: gestão de posts (gate de aprovação / edição / publicar agora) ──
        secured.MapPost("/social/posts/{id:guid}/approval", async (Guid id, SetApprovalRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SetPostApprovalCommand(id, request.Approved), ct)).ToHttp())
            .WithTags("Distribution")
            .WithSummary("Aprova/desaprova um post para publicação (gate)");

        secured.MapPut("/social/posts/{id:guid}/content", async (Guid id, EditPostRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new EditPostContentCommand(id, request.Caption, request.Hashtags), ct)).ToHttp())
            .WithTags("Distribution")
            .WithSummary("Edita a copy/hashtags de um post (antes de publicar)");

        secured.MapPost("/social/posts/{id:guid}/publish-now", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new PublishPostNowCommand(id), ct)).ToHttp())
            .WithTags("Distribution")
            .WithSummary("Publica o post imediatamente (aprova, enfileira e publica)");

        secured.MapGet("/products/{id:guid}/metrics", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetProductMetricsQuery(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Funil de métricas do produto (visitas → cliques → vendas, 30 dias)");

        secured.MapGet("/products/{id:guid}/provenance", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetProductProvenanceQuery(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Proveniência do PDF: quem gerou o texto (IA) e as imagens (Media Gateway)");

        secured.MapGet("/products/{id:guid}/audit", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetConversionAuditQuery(id), ct)).ToHttp())
            .WithTags("Products")
            .WithSummary("Auditoria de conversão por IA: pontua o e-book contra o checklist de persuasão (Fase 7)");

        secured.MapPost("/analytics/aggregate", async (IMetricsAggregator aggregator, CancellationToken ct) =>
        {
            var rows = await aggregator.AggregateAsync(DateTime.UtcNow.Date, ct);
            return Results.Ok(new { date = DateTime.UtcNow.Date, rows });
        })
            .WithTags("Analytics")
            .WithSummary("Dispara a agregação de métricas de hoje (MetricDaily)");

        secured.MapPost("/optimizer/run", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RunOptimizationCommand(), ct)).ToHttp())
            .WithTags("Optimizer")
            .WithSummary("Dispara o ciclo de otimização de ROI (classifica os produtos Live)");

        secured.MapGet("/optimizer/runs", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetOptimizationRunsQuery(), ct)).ToHttp())
            .WithTags("Optimizer")
            .WithSummary("Lista as execuções do otimizador");

        secured.MapGet("/optimizer/runs/{id:guid}/decisions", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetRunDecisionsQuery(id), ct)).ToHttp())
            .WithTags("Optimizer")
            .WithSummary("Decisões de uma execução (para revisão/veto)");

        secured.MapPost("/optimizer/decisions/{id:guid}/approve", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new ApproveDecisionCommand(id), ct)).ToHttp())
            .WithTags("Optimizer")
            .WithSummary("Aprova e executa uma decisão do otimizador");

        secured.MapPost("/optimizer/decisions/{id:guid}/veto", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new VetoDecisionCommand(id), ct)).ToHttp())
            .WithTags("Optimizer")
            .WithSummary("Veta uma decisão do otimizador (não executa)");

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

        // Laboratório de LP: gera uma landing page de teste a partir de um nicho (sem criar e-book),
        // injetando a "memória" (feedback) na copy; devolve HTML + o caminho percorrido.
        secured.MapPost("/lp-lab/generate", async (GenerateTestLpRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new GenerateTestLpCommand(request.NicheId, request.Feedback), ct)).ToHttp())
            .WithTags("LpLab")
            .WithSummary("Gera/regenera uma landing page de teste para um nicho");

        secured.MapPost("/lp-lab/memory", async (SaveLpMemoryRequest request, IFileStore fileStore, CancellationToken ct) =>
        {
            await fileStore.WriteTextAsync($"lp-lab/{request.NicheId}/memory.txt", request.Feedback ?? string.Empty, ct);
            return Results.NoContent();
        })
            .WithTags("LpLab")
            .WithSummary("Salva a memória (feedback) de teste de LP de um nicho");

        secured.MapGet("/lp-lab/memory", async (Guid nicheId, IFileStore fileStore, CancellationToken ct) =>
            Results.Ok(new { feedback = await fileStore.ReadTextAsync($"lp-lab/{nicheId}/memory.txt", ct) ?? string.Empty }))
            .WithTags("LpLab")
            .WithSummary("Lê a memória (feedback) de teste de LP de um nicho");
    }
}
