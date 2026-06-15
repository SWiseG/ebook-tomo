using Ebook.Application.Common.Messaging;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Optimization;

namespace Ebook.Application.Optimization;

/// <summary>Aprova uma decisão proposta e a executa imediatamente (E12-05).</summary>
public sealed record ApproveDecisionCommand(Guid DecisionId) : ICommand<bool>;

public sealed class ApproveDecisionCommandHandler(
    IOptimizationRepository optimization,
    IOptimizationExecutor executor,
    IUnitOfWork unitOfWork) : ICommandHandler<ApproveDecisionCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(ApproveDecisionCommand command, CancellationToken ct)
    {
        var decision = await optimization.GetDecisionAsync(command.DecisionId, ct);
        if (decision is null)
        {
            return Result.Failure<bool>(OptimizationErrors.DecisionNotFound(command.DecisionId));
        }

        var approved = decision.Approve();
        if (approved.IsFailure)
        {
            return Result.Failure<bool>(approved.Error);
        }

        var executed = await executor.ExecuteAsync(decision, ct);
        if (executed.IsFailure)
        {
            return Result.Failure<bool>(executed.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>Veta uma decisão proposta (não executa nenhuma ação).</summary>
public sealed record VetoDecisionCommand(Guid DecisionId) : ICommand<bool>;

public sealed class VetoDecisionCommandHandler(
    IOptimizationRepository optimization,
    IUnitOfWork unitOfWork) : ICommandHandler<VetoDecisionCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(VetoDecisionCommand command, CancellationToken ct)
    {
        var decision = await optimization.GetDecisionAsync(command.DecisionId, ct);
        if (decision is null)
        {
            return Result.Failure<bool>(OptimizationErrors.DecisionNotFound(command.DecisionId));
        }

        var vetoed = decision.Veto();
        if (vetoed.IsFailure)
        {
            return Result.Failure<bool>(vetoed.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>Dispara o ciclo de otimização manualmente (a mesma rotina do cron de 30 dias).</summary>
public sealed record RunOptimizationCommand : ICommand<Guid>;

public sealed class RunOptimizationCommandHandler(IOptimizationService service)
    : ICommandHandler<RunOptimizationCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(RunOptimizationCommand command, CancellationToken ct) =>
        service.RunCycleAsync(ct);
}
