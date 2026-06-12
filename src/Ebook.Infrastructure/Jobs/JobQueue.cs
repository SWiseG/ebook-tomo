using Ebook.Application.Common.Jobs;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Jobs;

public sealed class JobQueue(EbookDbContext db, IClock clock) : IJobQueue
{
    public async Task<Result> EnqueueAsync(JobRequest request, CancellationToken ct = default)
    {
        var exists = await db.Jobs.AnyAsync(j => j.IdempotencyKey == request.IdempotencyKey, ct);
        if (exists)
        {
            return Result.Success(); // idempotente: já enfileirado
        }

        db.Jobs.Add(new JobRecord
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            PayloadJson = request.PayloadJson,
            ProductId = request.ProductId,
            Status = JobStatus.Pending,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = clock.UtcNow,
            ScheduledAtUtc = request.ScheduledAtUtc ?? clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
