using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Content;
using Ebook.Application.Content.Lp.Lab;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Application.Tests.Content;

public class LpLabRunTests
{
    [Fact]
    public async Task Enqueue_cria_job_lp_lab_e_devolve_runId()
    {
        var niche = Niche.Discover("financas", "Finanças", 0.9, "{}", 1, DateTime.UtcNow);
        var queue = new FakeJobQueue();
        var handler = new EnqueueTestLpHandler(new FakeNicheRepo(niche), queue);

        var result = await handler.HandleAsync(new EnqueueTestLpCommand(niche.Id, "mais ousado"), default);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.RunId);
        Assert.NotNull(queue.Last);
        Assert.Equal(ContentJobs.LpLab, queue.Last!.Type);
        Assert.Equal(ContentJobs.LpLabKey(result.Value.RunId), queue.Last.IdempotencyKey);
    }

    [Fact]
    public async Task Enqueue_com_nicho_inexistente_falha_sem_enfileirar()
    {
        var queue = new FakeJobQueue();
        var handler = new EnqueueTestLpHandler(new FakeNicheRepo(null), queue);

        var result = await handler.HandleAsync(new EnqueueTestLpCommand(Guid.NewGuid(), null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("lplab.nicheNotFound", result.Error.Code);
        Assert.Null(queue.Last);
    }

    [Fact]
    public async Task Result_sem_arquivo_retorna_pending()
    {
        var handler = new GetTestLpResultHandler(new FakeFileStore());

        var result = await handler.HandleAsync(new GetTestLpResultQuery(Guid.NewGuid()), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Null(result.Value.Html);
    }

    [Fact]
    public async Task Job_sucesso_persiste_html_e_trace_lidos_pela_query()
    {
        var runId = Guid.NewGuid();
        var nicheId = Guid.NewGuid();
        var store = new FakeFileStore();
        var trace = new LpTraceDto("Finanças", "Money", "Modern", "#000", "#0af", "Inter", "Inter", "Título", true, []);
        var dispatcher = new FakeDispatcher(Result.Success(new GenerateTestLpResult("<html>ok</html>", trace)));
        var job = new LpLabJobHandler(dispatcher, store, NullLogger<LpLabJobHandler>.Instance);

        var jobResult = await job.ExecuteAsync(
            System.Text.Json.JsonSerializer.Serialize(new LpLabJobPayload(runId, nicheId, null)), default);
        Assert.True(jobResult.IsSuccess);

        var read = await new GetTestLpResultHandler(store)
            .HandleAsync(new GetTestLpResultQuery(runId), default);

        Assert.Equal("succeeded", read.Value.Status);
        Assert.Equal("<html>ok</html>", read.Value.Html);
        Assert.Equal("Título", read.Value.Trace!.Title);
    }

    [Fact]
    public async Task Job_falha_de_geracao_persiste_status_failed_e_termina_sem_retry()
    {
        var runId = Guid.NewGuid();
        var store = new FakeFileStore();
        var dispatcher = new FakeDispatcher(
            Result.Failure<GenerateTestLpResult>(new Error("ai.window", "sem cota")));
        var job = new LpLabJobHandler(dispatcher, store, NullLogger<LpLabJobHandler>.Instance);

        // Terminal: retorna sucesso (não reenfileira), mas grava o desfecho de falha.
        var jobResult = await job.ExecuteAsync(
            System.Text.Json.JsonSerializer.Serialize(new LpLabJobPayload(runId, Guid.NewGuid(), null)), default);
        Assert.True(jobResult.IsSuccess);

        var read = await new GetTestLpResultHandler(store)
            .HandleAsync(new GetTestLpResultQuery(runId), default);

        Assert.Equal("failed", read.Value.Status);
        Assert.Equal("sem cota", read.Value.Error);
    }

    private sealed class FakeNicheRepo(Niche? niche) : INicheRepository
    {
        public Task<Niche?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(niche);
        public Task<Niche?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(niche);
        public Task<IReadOnlyList<string>> ActiveSlugsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public void Add(Niche niche) { }
    }

    private sealed class FakeJobQueue : IJobQueue
    {
        public JobRequest? Last { get; private set; }
        public Task<Result> EnqueueAsync(JobRequest request, CancellationToken ct = default)
        {
            Last = request;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeFileStore : IFileStore
    {
        private readonly Dictionary<string, string> files = [];
        public Task<StoredFile> WriteTextAsync(string relativePath, string content, CancellationToken ct = default)
        {
            files[relativePath] = content;
            return Task.FromResult(new StoredFile(relativePath, "hash", content.Length));
        }
        public Task<string?> ReadTextAsync(string relativePath, CancellationToken ct = default) =>
            Task.FromResult(files.GetValueOrDefault(relativePath));
        public bool Exists(string relativePath) => files.ContainsKey(relativePath);
    }

    private sealed class FakeDispatcher(Result<GenerateTestLpResult> canned) : IDispatcher
    {
        public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
            Task.FromResult((Result<TResult>)(object)canned);
        public Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
