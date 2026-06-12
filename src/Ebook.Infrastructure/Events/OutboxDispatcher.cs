using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Ebook.Application.Common.Events;
using Ebook.Domain.Common;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Events;

/// <summary>Resolve o CLR type de um Domain Event pelo nome gravado no Outbox.</summary>
public static class DomainEventTypeRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> Types =
        typeof(IDomainEvent).Assembly.GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .ToDictionary(t => t.Name, t => t);

    public static Type? Resolve(string typeName) => Types.GetValueOrDefault(typeName);
}

/// <summary>
/// Lê eventos pendentes do Outbox e despacha para os IDomainEventHandler registrados.
/// Entrega at-least-once; idempotência por (EventId, HandlerName) em ProcessedEvent.
/// Evento com 5 falhas vira poison (ProcessedAt preenchido com Error) e fica visível no painel.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethodCache = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxDispatcher iniciado");
        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await ProcessPendingOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no loop do OutboxDispatcher");
                processed = 0;
            }

            await IdleDelay(processed, stoppingToken);
        }
    }

    private static async Task IdleDelay(int processed, CancellationToken ct)
    {
        try
        {
            await Task.Delay(processed > 0 ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(1), ct);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    /// <summary>Uma passada de processamento; público para testes de integração.</summary>
    public async Task<int> ProcessPendingOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();

        var pending = await db.OutboxEvents
            .Where(e => e.ProcessedAtUtc == null)
            .OrderBy(e => e.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var record in pending)
        {
            await DispatchRecordAsync(scope.ServiceProvider, db, record, ct);
            await db.SaveChangesAsync(ct);
        }

        return pending.Count;
    }

    private async Task DispatchRecordAsync(IServiceProvider services, EbookDbContext db, OutboxEventRecord record, CancellationToken ct)
    {
        var eventType = DomainEventTypeRegistry.Resolve(record.Type);
        if (eventType is null)
        {
            record.ProcessedAtUtc = DateTime.UtcNow;
            record.Error = $"Tipo de evento desconhecido: {record.Type}";
            logger.LogError("Evento {EventId} com tipo desconhecido {EventType}", record.Id, record.Type);
            return;
        }

        try
        {
            var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(record.PayloadJson, eventType, JsonOptions)!;
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var handlers = services.GetServices(handlerType).Where(h => h is not null).ToList();

            foreach (var handler in handlers)
            {
                var handlerName = handler!.GetType().FullName!;
                var alreadyProcessed = await db.ProcessedEvents
                    .AnyAsync(p => p.EventId == record.Id && p.HandlerName == handlerName, ct);
                if (alreadyProcessed)
                {
                    continue;
                }

                var method = HandleMethodCache.GetOrAdd(handlerType, t => t.GetMethod("HandleAsync")!);
                await (Task)method.Invoke(handler, [domainEvent, ct])!;

                db.ProcessedEvents.Add(new ProcessedEventRecord
                {
                    EventId = record.Id,
                    HandlerName = handlerName,
                    ProcessedAtUtc = DateTime.UtcNow
                });
            }

            record.ProcessedAtUtc = DateTime.UtcNow;
            record.Error = null;
        }
        catch (Exception ex)
        {
            record.Attempts++;
            var detail = ex is TargetInvocationException { InnerException: not null } tie
                ? tie.InnerException!.Message
                : ex.Message;
            record.Error = detail;
            logger.LogError(ex, "Falha ao despachar evento {EventId} ({EventType}), tentativa {Attempt}",
                record.Id, record.Type, record.Attempts);

            if (record.Attempts >= MaxAttempts)
            {
                record.ProcessedAtUtc = DateTime.UtcNow;
                logger.LogError("Evento {EventId} marcado como poison após {MaxAttempts} tentativas", record.Id, MaxAttempts);
            }
        }
    }
}
