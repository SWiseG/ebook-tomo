using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content.Lp.Lab;

/// <summary>
/// Versão assíncrona do laboratório de LP. A geração (copy + capa + ilustração por IA) leva
/// minutos e estourava o timeout do proxy reverso (~120s) quando feita na request HTTP. Aqui o
/// endpoint só enfileira um job <see cref="ContentJobs.LpLab"/> e devolve um <c>RunId</c>; o job
/// roda com o token de vida da aplicação (não o da request), reusa o
/// <see cref="GenerateTestLpHandler"/> e persiste o resultado no FileStore, que o painel busca por
/// polling. Resolve a classe inteira de timeouts HTTP em geração síncrona de IA.
/// </summary>
public static class LpLabPaths
{
    public static string Run(Guid runId) => $"lp-lab/runs/{runId}.json";
}

/// <summary>Estado de um run de teste de LP. <c>Status</c>: pending | succeeded | failed.</summary>
public sealed record LpLabRunDto(string Status, string? Html, LpTraceDto? Trace, string? Error);

/// <summary>Enfileira um run de teste de LP e devolve o id para o painel acompanhar.</summary>
public sealed record EnqueueTestLpCommand(Guid NicheId, string? Feedback) : ICommand<EnqueueTestLpResult>;

public sealed record EnqueueTestLpResult(Guid RunId);

public sealed class EnqueueTestLpHandler(INicheRepository niches, IJobQueue jobQueue)
    : ICommandHandler<EnqueueTestLpCommand, EnqueueTestLpResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<EnqueueTestLpResult>> HandleAsync(EnqueueTestLpCommand command, CancellationToken ct)
    {
        // Validação cedo: 404 imediato é melhor UX do que um run que falha minutos depois.
        var niche = await niches.GetByIdAsync(command.NicheId, ct);
        if (niche is null)
        {
            return Result.Failure<EnqueueTestLpResult>(new Error("lplab.nicheNotFound", "Nicho não encontrado."));
        }

        var runId = Guid.NewGuid();
        var enqueue = await jobQueue.EnqueueAsync(new JobRequest(
            ContentJobs.LpLab,
            JsonSerializer.Serialize(new LpLabJobPayload(runId, command.NicheId, command.Feedback), JsonOptions),
            ContentJobs.LpLabKey(runId)), ct);

        return enqueue.IsFailure
            ? Result.Failure<EnqueueTestLpResult>(enqueue.Error)
            : Result.Success(new EnqueueTestLpResult(runId));
    }
}

/// <summary>Lê o resultado de um run. Ausente = ainda em processamento (pending).</summary>
public sealed record GetTestLpResultQuery(Guid RunId) : IQuery<LpLabRunDto>;

public sealed class GetTestLpResultHandler(IFileStore fileStore)
    : IQueryHandler<GetTestLpResultQuery, LpLabRunDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<LpLabRunDto>> HandleAsync(GetTestLpResultQuery query, CancellationToken ct)
    {
        var json = await fileStore.ReadTextAsync(LpLabPaths.Run(query.RunId), ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Success(new LpLabRunDto("pending", null, null, null));
        }

        var run = JsonSerializer.Deserialize<LpLabRunDto>(json, JsonOptions);
        return run is null
            ? Result.Success(new LpLabRunDto("pending", null, null, null))
            : Result.Success(run);
    }
}

/// <summary>
/// Executa o run: reusa o <see cref="GenerateTestLpCommand"/> síncrono (toda a orquestração já
/// existente) com o token do worker e grava o desfecho no FileStore. Sempre termina em sucesso
/// (terminal): falha de geração vira um resultado "failed" persistido — não faz sentido o retry
/// automático da fila numa ferramenta interativa; o operador reenfileira clicando "gerar".
/// </summary>
public sealed class LpLabJobHandler(
    IDispatcher dispatcher,
    IFileStore fileStore,
    ILogger<LpLabJobHandler> logger) : IJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => ContentJobs.LpLab;

    public async Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<LpLabJobPayload>(payloadJson, JsonOptions)!;

        var generated = await dispatcher.SendAsync(
            new GenerateTestLpCommand(payload.NicheId, payload.Feedback), ct);

        var run = generated.IsSuccess
            ? new LpLabRunDto("succeeded", generated.Value.Html, generated.Value.Trace, null)
            : new LpLabRunDto("failed", null, null, generated.Error.Message);

        await fileStore.WriteTextAsync(
            LpLabPaths.Run(payload.RunId), JsonSerializer.Serialize(run, JsonOptions), ct);

        if (generated.IsFailure)
        {
            logger.LogWarning("Run de LP {RunId} falhou: {Error}", payload.RunId, generated.Error);
        }

        return Result.Success();
    }
}
