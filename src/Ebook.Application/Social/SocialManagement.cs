using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Social;

namespace Ebook.Application.Social;

// ───────────────────────── Canais (1 por nicho) ─────────────────────────

public sealed record ChannelDto(
    Guid Id,
    Guid NicheId,
    string NicheName,
    string Name,
    string Platform,
    bool Connected,
    string? PageId,
    string? IgUserId,
    bool HasToken,
    string? PublicMediaBaseUrl,
    DateTime? TokenExpiresAtUtc,
    DateTime CreatedAtUtc);

public sealed record GetChannelsQuery : IQuery<IReadOnlyList<ChannelDto>>;

public sealed class GetChannelsQueryHandler(IChannelRepository channels, INicheRepository niches)
    : IQueryHandler<GetChannelsQuery, IReadOnlyList<ChannelDto>>
{
    public async Task<Result<IReadOnlyList<ChannelDto>>> HandleAsync(GetChannelsQuery query, CancellationToken ct)
    {
        var list = await channels.ListAsync(ct);
        var dtos = new List<ChannelDto>(list.Count);
        foreach (var c in list)
        {
            var niche = await niches.GetByIdAsync(c.NicheId, ct);
            // Nunca expõe o token; só sinaliza presença/validade.
            dtos.Add(new ChannelDto(
                c.Id, c.NicheId, niche?.Name ?? "—", c.Name, c.Platform.ToString(),
                c.IsConnected, c.PageId, c.IgUserId, !string.IsNullOrWhiteSpace(c.AccessToken),
                c.PublicMediaBaseUrl, c.TokenExpiresAtUtc, c.CreatedAtUtc));
        }

        return Result.Success<IReadOnlyList<ChannelDto>>(dtos);
    }
}

public sealed record CreateChannelCommand(Guid NicheId, string Name) : ICommand<Guid>;

public sealed class CreateChannelCommandHandler(
    IChannelRepository channels, INicheRepository niches, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateChannelCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateChannelCommand command, CancellationToken ct)
    {
        var niche = await niches.GetByIdAsync(command.NicheId, ct);
        if (niche is null)
        {
            return Result.Failure<Guid>(SocialErrorsApp.NicheNotFound(command.NicheId));
        }

        if (await channels.GetByNicheAsync(command.NicheId, ct) is not null)
        {
            return Result.Failure<Guid>(SocialErrorsApp.ChannelExists);
        }

        var name = string.IsNullOrWhiteSpace(command.Name) ? niche.Name : command.Name.Trim();
        var channel = Channel.Create(command.NicheId, name, ChannelPlatform.Meta, clock.UtcNow);
        channels.Add(channel);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(channel.Id);
    }
}

/// <summary>Conecta/atualiza credenciais do Meta no canal (token de longa duração + ids).</summary>
public sealed record ConnectChannelCommand(
    Guid ChannelId,
    string? Name,
    string? PageId,
    string? IgUserId,
    string AccessToken,
    string? PublicMediaBaseUrl,
    DateTime? TokenExpiresAtUtc) : ICommand<bool>;

public sealed class ConnectChannelCommandHandler(IChannelRepository channels, IUnitOfWork unitOfWork)
    : ICommandHandler<ConnectChannelCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(ConnectChannelCommand command, CancellationToken ct)
    {
        var channel = await channels.GetByIdAsync(command.ChannelId, ct);
        if (channel is null)
        {
            return Result.Failure<bool>(SocialErrorsApp.ChannelNotFound(command.ChannelId));
        }

        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            channel.Rename(command.Name.Trim());
        }

        channel.SetCredentials(
            command.PageId, command.IgUserId, command.AccessToken,
            command.PublicMediaBaseUrl, command.TokenExpiresAtUtc);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ───────────────────── Posts: gate de aprovação, edição e publicar agora ─────────────────────

public sealed record SetPostApprovalCommand(Guid PostId, bool Approved) : ICommand<bool>;

public sealed class SetPostApprovalCommandHandler(ISocialPostRepository posts, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SetPostApprovalCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(SetPostApprovalCommand command, CancellationToken ct)
    {
        var post = await posts.GetByIdAsync(command.PostId, ct);
        if (post is null)
        {
            return Result.Failure<bool>(SocialErrorsApp.PostNotFound(command.PostId));
        }

        if (command.Approved)
        {
            var result = post.Approve(clock.UtcNow);
            if (result.IsFailure)
            {
                return Result.Failure<bool>(result.Error);
            }
        }
        else
        {
            post.Unapprove();
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

public sealed record EditPostContentCommand(Guid PostId, string Caption, string Hashtags) : ICommand<bool>;

public sealed class EditPostContentCommandHandler(ISocialPostRepository posts, IUnitOfWork unitOfWork)
    : ICommandHandler<EditPostContentCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(EditPostContentCommand command, CancellationToken ct)
    {
        var post = await posts.GetByIdAsync(command.PostId, ct);
        if (post is null)
        {
            return Result.Failure<bool>(SocialErrorsApp.PostNotFound(command.PostId));
        }

        var result = post.EditContent(command.Caption, command.Hashtags);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>Publica o post imediatamente (aprova se preciso, marca Queued e enfileira a publicação).</summary>
public sealed record PublishPostNowCommand(Guid PostId) : ICommand<bool>;

public sealed class PublishPostNowCommandHandler(
    ISocialPostRepository posts, IJobQueue jobQueue, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<PublishPostNowCommand, bool>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<bool>> HandleAsync(PublishPostNowCommand command, CancellationToken ct)
    {
        var post = await posts.GetByIdAsync(command.PostId, ct);
        if (post is null)
        {
            return Result.Failure<bool>(SocialErrorsApp.PostNotFound(command.PostId));
        }

        if (post.Status != SocialPostStatus.Planned)
        {
            return Result.Failure<bool>(SocialErrorsApp.PostNotEditable);
        }

        post.Approve(clock.UtcNow);
        var queued = post.Queue();
        if (queued.IsFailure)
        {
            return Result.Failure<bool>(queued.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await jobQueue.EnqueueAsync(new JobRequest(
            SocialJobs.Publish,
            JsonSerializer.Serialize(new PublishPostJobPayload(post.Id), JsonOptions),
            SocialJobs.PublishKey(post.Id),
            ProductId: post.ProductId), ct);
        return Result.Success(true);
    }
}
