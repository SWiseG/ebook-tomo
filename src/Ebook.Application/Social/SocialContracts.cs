using Ebook.Domain.Common;
using Ebook.Domain.Social;

namespace Ebook.Application.Social;

/// <summary>Tipos/chaves dos jobs sociais (E08).</summary>
public static class SocialJobs
{
    public const string Calendar = "social.calendar";
    public const string Dispatch = "social.dispatch";
    public const string Publish = "social.publish";

    public static string CalendarKey(Guid productId) => $"social-calendar:{productId}";
    public static string DispatchKey(DateTime dayUtc) => $"social-dispatch:{dayUtc:yyyyMMdd}";
    public static string PublishKey(Guid postId) => $"social-publish:{postId}";
}

public sealed record CalendarJobPayload(Guid ProductId);

public sealed record DispatchJobPayload(DateTime AsOfUtc);

public sealed record PublishPostJobPayload(Guid PostId);

/// <summary>Saída da IA para o calendário: lista de posts ao longo de ~30 dias.</summary>
public sealed record CalendarPlanDto(IReadOnlyList<CalendarPostDto> Posts);

public sealed record CalendarPostDto(
    int Day,
    string Network,
    string PostType,
    string Headline,
    string Copy,
    IReadOnlyList<string>? Hashtags,
    string? TimeSlot);

public sealed record SocialPublishRequest(SocialNetwork Network, string Caption, string? MediaPath, string? LinkUrl);

public sealed record SocialPublishOutcome(string ExternalId);

/// <summary>
/// Publicação em rede social (E08). Implementação real (Meta Graph API) é uma costura gated:
/// sem token configurado, falha de forma tipada e o post fica visível como Falhou no painel.
/// </summary>
public interface ISocialPublisher
{
    Task<Result<SocialPublishOutcome>> PublishAsync(SocialPublishRequest request, CancellationToken ct);
}

public static class SocialErrorsApp
{
    public static Error ProductNotFound(Guid id) =>
        new("Social.Product.NotFound", $"Produto {id} não encontrado.");

    public static Error NotConfigured =>
        new("Social.NotConfigured", "Integração Meta (Instagram/Facebook) não configurada.");

    public static Error AutomationPending =>
        new("Social.AutomationPending", "Publicação automática no Meta ainda não implementada.");
}
