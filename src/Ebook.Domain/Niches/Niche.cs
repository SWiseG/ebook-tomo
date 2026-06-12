using Ebook.Domain.Common;

namespace Ebook.Domain.Niches;

public enum NicheStatus
{
    Candidate,
    Selected,
    Active,
    Discarded
}

public sealed class Niche : AggregateRoot
{
    private Niche()
    {
        Slug = string.Empty;
        Name = string.Empty;
        ScoreBreakdownJson = "{}";
    }

    public string Slug { get; private set; }
    public string Name { get; private set; }
    public NicheStatus Status { get; private set; }
    public double Score { get; private set; }
    public string ScoreBreakdownJson { get; private set; }
    public DateTime DiscoveredAtUtc { get; private set; }
    public int CycleNumber { get; private set; }

    public static Niche Discover(string slug, string name, double score, string scoreBreakdownJson, int cycleNumber, DateTime utcNow)
    {
        var niche = new Niche
        {
            Slug = slug,
            Name = name,
            Status = NicheStatus.Candidate,
            Score = score,
            ScoreBreakdownJson = scoreBreakdownJson,
            DiscoveredAtUtc = utcNow,
            CycleNumber = cycleNumber
        };
        niche.Raise(new NicheDiscovered(niche.Id, slug, score));
        return niche;
    }

    public Result Select()
    {
        if (Status != NicheStatus.Candidate)
        {
            return Result.Failure(NicheErrors.InvalidTransition(Status, NicheStatus.Selected));
        }

        Status = NicheStatus.Selected;
        Raise(new NicheSelected(Id, Slug));
        return Result.Success();
    }

    public Result Activate()
    {
        if (Status != NicheStatus.Selected)
        {
            return Result.Failure(NicheErrors.InvalidTransition(Status, NicheStatus.Active));
        }

        Status = NicheStatus.Active;
        return Result.Success();
    }

    public Result Discard()
    {
        if (Status is NicheStatus.Active or NicheStatus.Discarded)
        {
            return Result.Failure(NicheErrors.InvalidTransition(Status, NicheStatus.Discarded));
        }

        Status = NicheStatus.Discarded;
        return Result.Success();
    }
}

public static class NicheErrors
{
    public static Error InvalidTransition(NicheStatus from, NicheStatus to) =>
        new("Niche.InvalidTransition", $"Transição inválida de {from} para {to}.");
}

public sealed record NicheDiscovered(Guid NicheId, string Slug, double Score) : DomainEvent;

public sealed record NicheSelected(Guid NicheId, string Slug) : DomainEvent;
