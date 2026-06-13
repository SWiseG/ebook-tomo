using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;

namespace Ebook.Application.Discovery;

public static class NicheErrorsApp
{
    public static Error NotFound(Guid id) => new("Niche.NotFound", $"Nicho {id} não encontrado.");
}

/// <summary>Aprova um nicho candidato (Selected) — gate humano do painel (E02-07).</summary>
public sealed record ApproveNicheCommand(Guid NicheId) : ICommand<bool>;

public sealed class ApproveNicheCommandHandler(INicheRepository niches, IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveNicheCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(ApproveNicheCommand command, CancellationToken ct)
    {
        var niche = await niches.GetByIdAsync(command.NicheId, ct);
        if (niche is null)
        {
            return Result.Failure<bool>(NicheErrorsApp.NotFound(command.NicheId));
        }

        var result = niche.Select();
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

public sealed record DiscardNicheCommand(Guid NicheId) : ICommand<bool>;

public sealed class DiscardNicheCommandHandler(INicheRepository niches, IUnitOfWork unitOfWork)
    : ICommandHandler<DiscardNicheCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(DiscardNicheCommand command, CancellationToken ct)
    {
        var niche = await niches.GetByIdAsync(command.NicheId, ct);
        if (niche is null)
        {
            return Result.Failure<bool>(NicheErrorsApp.NotFound(command.NicheId));
        }

        var result = niche.Discard();
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>Dispara a descoberta manualmente (a mesma do cron de 30 dias) enfileirando o job.</summary>
public sealed record DiscoverNichesCommand(int? TopN = null) : ICommand<bool>;

public sealed class DiscoverNichesCommandHandler(IJobQueue jobQueue, IClock clock)
    : ICommandHandler<DiscoverNichesCommand, bool>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<bool>> HandleAsync(DiscoverNichesCommand command, CancellationToken ct)
    {
        var cycle = (clock.UtcNow.Year - 2026) * 12 + clock.UtcNow.Month;
        await jobQueue.EnqueueAsync(new JobRequest(
            DiscoveryJobs.Discover,
            JsonSerializer.Serialize(new DiscoverNichesJobPayload(command.TopN), JsonOptions),
            DiscoveryJobs.DiscoverKey(cycle)), ct);
        return Result.Success(true);
    }
}
