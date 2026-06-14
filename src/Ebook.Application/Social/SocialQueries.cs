using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;
using Ebook.Domain.Social;

namespace Ebook.Application.Social;

public sealed record SocialPostDto(
    Guid Id,
    int Day,
    string Network,
    string PostType,
    string Caption,
    string Status,
    DateTime ScheduledAtUtc,
    DateTime? PublishedAtUtc,
    string? ExternalId);

/// <summary>Agenda social de um produto, ordenada por dia (para o painel).</summary>
public sealed record GetProductSocialQuery(Guid ProductId) : IQuery<IReadOnlyList<SocialPostDto>>;

public sealed class GetProductSocialQueryHandler(ISocialPostRepository posts)
    : IQueryHandler<GetProductSocialQuery, IReadOnlyList<SocialPostDto>>
{
    public async Task<Result<IReadOnlyList<SocialPostDto>>> HandleAsync(GetProductSocialQuery query, CancellationToken ct)
    {
        var list = await posts.GetByProductAsync(query.ProductId, ct);
        IReadOnlyList<SocialPostDto> dtos = list
            .Select(p => new SocialPostDto(
                p.Id, p.Day, p.Network.ToString(), p.PostType.ToString(), p.Caption,
                p.Status.ToString(), p.ScheduledAtUtc, p.PublishedAtUtc, p.ExternalId))
            .ToList();
        return Result.Success(dtos);
    }
}
