using Ebook.Domain.Common;

namespace Ebook.Domain.Social;

public enum SocialNetwork
{
    Instagram,
    Facebook,
    X
}

public enum SocialPostType
{
    Launch,
    Value,
    Proof,
    Offer,
    Reel
}

public enum SocialPostStatus
{
    Planned,
    Queued,
    Published,
    Failed,
    Skipped
}

/// <summary>
/// Item do calendário de conteúdo de um produto (E08). O plano completo fica no FileStore
/// (calendar.json, em <see cref="ContentPath"/>); o card renderizado em <see cref="MediaPath"/>.
/// Transições de status validadas; o job de publicação leva Queued → Published/Failed.
/// </summary>
public sealed class SocialPost : Entity
{
    private SocialPost()
    {
        Caption = string.Empty;
        Hashtags = string.Empty;
        ContentPath = string.Empty;
        Utm = string.Empty;
        MetricsJson = "{}";
    }

    public Guid ProductId { get; private set; }
    public SocialNetwork Network { get; private set; }
    public SocialPostType PostType { get; private set; }
    public int Day { get; private set; }
    public string Caption { get; private set; }
    public string Hashtags { get; private set; }
    public string ContentPath { get; private set; }
    public string? MediaPath { get; private set; }
    public string Utm { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public string? ExternalId { get; private set; }
    public SocialPostStatus Status { get; private set; }
    public string MetricsJson { get; private set; }

    public static SocialPost Plan(
        Guid productId,
        SocialNetwork network,
        SocialPostType postType,
        int day,
        string caption,
        string hashtags,
        string contentPath,
        string utm,
        DateTime scheduledAtUtc) =>
        new()
        {
            ProductId = productId,
            Network = network,
            PostType = postType,
            Day = day,
            Caption = caption,
            Hashtags = hashtags,
            ContentPath = contentPath,
            Utm = utm,
            ScheduledAtUtc = scheduledAtUtc,
            Status = SocialPostStatus.Planned
        };

    public void SetMedia(string mediaPath) => MediaPath = mediaPath;

    public Result Queue()
    {
        if (Status != SocialPostStatus.Planned)
        {
            return Result.Failure(SocialErrors.InvalidTransition(Status, SocialPostStatus.Queued));
        }

        Status = SocialPostStatus.Queued;
        return Result.Success();
    }

    public Result MarkPublished(string externalId, DateTime utcNow)
    {
        if (Status is not (SocialPostStatus.Queued or SocialPostStatus.Planned))
        {
            return Result.Failure(SocialErrors.InvalidTransition(Status, SocialPostStatus.Published));
        }

        Status = SocialPostStatus.Published;
        ExternalId = externalId;
        PublishedAtUtc = utcNow;
        return Result.Success();
    }

    public void MarkFailed() => Status = SocialPostStatus.Failed;
}

public static class SocialErrors
{
    public static Error InvalidTransition(SocialPostStatus from, SocialPostStatus to) =>
        new("Social.InvalidTransition", $"Transição inválida de {from} para {to}.");
}
