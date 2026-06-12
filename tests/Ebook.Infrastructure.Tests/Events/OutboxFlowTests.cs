using Ebook.Application.Common.Events;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Niches;
using Ebook.Infrastructure.Events;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Tests.Events;

public class OutboxFlowTests
{
    private sealed class CountingHandler : IDomainEventHandler<NicheDiscovered>
    {
        public int Invocations;
        public List<string> SlugsReceived { get; } = [];

        public Task HandleAsync(NicheDiscovered domainEvent, CancellationToken ct)
        {
            Interlocked.Increment(ref Invocations);
            SlugsReceived.Add(domainEvent.Slug);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IDomainEventHandler<NicheDiscovered>
    {
        public Task HandleAsync(NicheDiscovered domainEvent, CancellationToken ct) =>
            throw new InvalidOperationException("handler quebrado");
    }

    private static async Task<Guid> SaveNicheAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var niche = Niche.Discover("nicho-teste", "Nicho Teste", 0.9, "{}", 1, DateTime.UtcNow);
        db.Niches.Add(niche);
        await uow.SaveChangesAsync();
        return niche.Id;
    }

    [Fact]
    public async Task SaveChanges_grava_evento_no_outbox_na_mesma_transacao()
    {
        using var provider = TestHost.Build();
        await SaveNicheAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var outbox = await db.OutboxEvents.SingleAsync();

        Assert.Equal(nameof(NicheDiscovered), outbox.Type);
        Assert.Null(outbox.ProcessedAtUtc);
        Assert.Contains("nicho-teste", outbox.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatcher_entrega_ao_handler_e_reentrega_e_noop()
    {
        var handler = new CountingHandler();
        using var provider = TestHost.Build(s =>
            s.AddSingleton<IDomainEventHandler<NicheDiscovered>>(handler));
        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<OutboxDispatcher>>());

        await SaveNicheAsync(provider);

        var processedFirstPass = await dispatcher.ProcessPendingOnceAsync(CancellationToken.None);
        var processedSecondPass = await dispatcher.ProcessPendingOnceAsync(CancellationToken.None);

        Assert.Equal(1, processedFirstPass);
        Assert.Equal(0, processedSecondPass);
        Assert.Equal(1, handler.Invocations);
        Assert.Equal(["nicho-teste"], handler.SlugsReceived);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.NotNull((await db.OutboxEvents.SingleAsync()).ProcessedAtUtc);
        Assert.Equal(1, await db.ProcessedEvents.CountAsync());
    }

    [Fact]
    public async Task Handler_que_falha_vira_poison_apos_5_tentativas()
    {
        using var provider = TestHost.Build(s =>
            s.AddSingleton<IDomainEventHandler<NicheDiscovered>>(new ThrowingHandler()));
        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<OutboxDispatcher>>());

        await SaveNicheAsync(provider);

        for (var i = 0; i < 5; i++)
        {
            await dispatcher.ProcessPendingOnceAsync(CancellationToken.None);
        }

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var record = await db.OutboxEvents.SingleAsync();

        Assert.Equal(5, record.Attempts);
        Assert.NotNull(record.ProcessedAtUtc); // poison: sai da fila mas mantém o erro visível
        Assert.Contains("handler quebrado", record.Error, StringComparison.Ordinal);
    }
}
