using Ebook.Application.Common.Jobs;
using Ebook.Domain.Common;
using Ebook.Infrastructure.Jobs;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Tests.Jobs;

public class JobQueueWorkerTests
{
    private sealed class OkHandler : IJobHandler
    {
        public int Executions;
        public string Type => "test.ok";

        public Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct)
        {
            Interlocked.Increment(ref Executions);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FailHandler : IJobHandler
    {
        public string Type => "test.fail";

        public Task<Result> ExecuteAsync(string payloadJson, CancellationToken ct) =>
            Task.FromResult(Result.Failure(new Error("Test.Boom", "falha proposital")));
    }

    private static JobWorker BuildWorker(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(),
        provider.GetRequiredService<ILogger<JobWorker>>());

    private static async Task EnqueueAsync(ServiceProvider provider, string type, string key)
    {
        using var scope = provider.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
        await queue.EnqueueAsync(new JobRequest(type, "{}", key));
    }

    private static async Task MakeDueNowAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        await db.Jobs.Where(j => j.Status == JobStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.ScheduledAtUtc, DateTime.UtcNow.AddSeconds(-1)));
    }

    private static ServiceProvider BuildProvider(params IJobHandler[] handlers) =>
        TestHost.Build(s =>
        {
            s.AddScoped<IJobQueue, JobQueue>();
            foreach (var handler in handlers)
            {
                s.AddSingleton(handler);
            }
        });

    [Fact]
    public async Task Enqueue_com_mesma_idempotency_key_nao_duplica()
    {
        using var provider = BuildProvider();
        await EnqueueAsync(provider, "test.ok", "chave-1");
        await EnqueueAsync(provider, "test.ok", "chave-1");

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(1, await db.Jobs.CountAsync());
    }

    [Fact]
    public async Task Worker_executa_job_com_sucesso()
    {
        var handler = new OkHandler();
        using var provider = BuildProvider(handler);
        await EnqueueAsync(provider, "test.ok", "ok-1");

        var processed = await BuildWorker(provider).ProcessNextAsync(CancellationToken.None);

        Assert.True(processed);
        Assert.Equal(1, handler.Executions);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var job = await db.Jobs.SingleAsync();
        Assert.Equal(JobStatus.Succeeded, job.Status);
    }

    [Fact]
    public async Task Job_sem_handler_vai_direto_para_dead()
    {
        using var provider = BuildProvider();
        await EnqueueAsync(provider, "tipo.inexistente", "x-1");

        await BuildWorker(provider).ProcessNextAsync(CancellationToken.None);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var job = await db.Jobs.SingleAsync();
        Assert.Equal(JobStatus.Dead, job.Status);
        Assert.Contains("Nenhum IJobHandler", job.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Job_que_falha_faz_retry_com_backoff_e_morre_na_terceira()
    {
        using var provider = BuildProvider(new FailHandler());
        var worker = BuildWorker(provider);
        await EnqueueAsync(provider, "test.fail", "fail-1");

        // tentativa 1: volta para Pending com backoff futuro
        await worker.ProcessNextAsync(CancellationToken.None);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
            var job = await db.Jobs.SingleAsync();
            Assert.Equal(JobStatus.Pending, job.Status);
            Assert.Equal(1, job.Attempts);
            Assert.True(job.ScheduledAtUtc > DateTime.UtcNow);
        }

        // tentativas 2 e 3 (antecipa o agendamento)
        await MakeDueNowAsync(provider);
        await worker.ProcessNextAsync(CancellationToken.None);
        await MakeDueNowAsync(provider);
        await worker.ProcessNextAsync(CancellationToken.None);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
            var job = await db.Jobs.SingleAsync();
            Assert.Equal(JobStatus.Dead, job.Status);
            Assert.Equal(3, job.Attempts);
            Assert.Contains("Test.Boom", job.LastError, StringComparison.Ordinal);
        }
    }
}
