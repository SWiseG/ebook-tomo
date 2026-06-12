using System.Text.Json;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Persistence;

public sealed class UnitOfWork(EbookDbContext dbContext, IClock clock) : IUnitOfWork
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var aggregates = dbContext.ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                dbContext.OutboxEvents.Add(new OutboxEventRecord
                {
                    Id = domainEvent.EventId,
                    Type = domainEvent.GetType().Name,
                    PayloadJson = JsonSerializer.Serialize((object)domainEvent, domainEvent.GetType(), JsonOptions),
                    CreatedAtUtc = clock.UtcNow
                });
            }

            aggregate.ClearDomainEvents();
        }

        return await dbContext.SaveChangesAsync(ct);
    }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
