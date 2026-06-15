using Ebook.Domain.Common;

namespace Ebook.Domain.Optimization;

public enum OptimizationRunStatus
{
    Running,
    Completed
}

public enum OptimizationDecisionKind
{
    Scale,
    Keep,
    Iterate,
    Kill
}

public enum OptimizationDecisionStatus
{
    Proposed,
    Approved,
    Executed,
    Vetoed
}

/// <summary>
/// Execução de um ciclo de otimização (E12). Uma por ciclo (chave natural <see cref="CycleNumber"/>).
/// O relatório de aprendizados fica no FileStore (<see cref="ReportPath"/>).
/// </summary>
public sealed class OptimizationRun : Entity
{
    private OptimizationRun()
    {
        ReportPath = string.Empty;
    }

    public int CycleNumber { get; private set; }
    public DateTime ExecutedAtUtc { get; private set; }
    public string ReportPath { get; private set; }
    public OptimizationRunStatus Status { get; private set; }

    public static OptimizationRun Start(int cycleNumber, DateTime utcNow) =>
        new()
        {
            CycleNumber = cycleNumber,
            ExecutedAtUtc = utcNow,
            Status = OptimizationRunStatus.Running
        };

    public void Complete(string reportPath)
    {
        ReportPath = reportPath;
        Status = OptimizationRunStatus.Completed;
    }
}

/// <summary>
/// Decisão do otimizador para um produto (escalar/manter/iterar/matar). Proposta por padrão;
/// o operador aprova/veta no painel (E12-05), salvo modo autônomo (roi.autoExecute).
/// </summary>
public sealed class OptimizationDecision : Entity
{
    private OptimizationDecision()
    {
        RationaleJson = "{}";
        ActionsJson = "{}";
    }

    public Guid RunId { get; private set; }
    public Guid ProductId { get; private set; }
    public OptimizationDecisionKind Decision { get; private set; }
    public string RationaleJson { get; private set; }
    public string ActionsJson { get; private set; }
    public OptimizationDecisionStatus Status { get; private set; }

    public static OptimizationDecision Propose(
        Guid runId, Guid productId, OptimizationDecisionKind decision, string rationaleJson, string actionsJson) =>
        new()
        {
            RunId = runId,
            ProductId = productId,
            Decision = decision,
            RationaleJson = rationaleJson,
            ActionsJson = actionsJson,
            Status = OptimizationDecisionStatus.Proposed
        };

    public Result Approve()
    {
        if (Status != OptimizationDecisionStatus.Proposed)
        {
            return Result.Failure(OptimizationErrors.NotProposed(Status));
        }

        Status = OptimizationDecisionStatus.Approved;
        return Result.Success();
    }

    public Result Veto()
    {
        if (Status != OptimizationDecisionStatus.Proposed)
        {
            return Result.Failure(OptimizationErrors.NotProposed(Status));
        }

        Status = OptimizationDecisionStatus.Vetoed;
        return Result.Success();
    }

    public Result MarkExecuted()
    {
        if (Status != OptimizationDecisionStatus.Approved)
        {
            return Result.Failure(OptimizationErrors.NotApproved(Status));
        }

        Status = OptimizationDecisionStatus.Executed;
        return Result.Success();
    }
}

public static class OptimizationErrors
{
    public static Error NotProposed(OptimizationDecisionStatus status) =>
        new("Optimization.NotProposed", $"Decisão com status {status} não pode ser aprovada/vetada.");

    public static Error NotApproved(OptimizationDecisionStatus status) =>
        new("Optimization.NotApproved", $"Decisão com status {status} não pode ser executada.");

    public static Error DecisionNotFound(Guid id) =>
        new("Optimization.Decision.NotFound", $"Decisão {id} não encontrada.");
}
