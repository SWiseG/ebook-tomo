using Ebook.Application.Publishing;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>Publisher Kiwify determinístico para testes: sempre sucesso, sem rede/Playwright.</summary>
public sealed class FakeKiwifyPublisher : IKiwifyPublisher
{
    public int Count { get; private set; }
    public KiwifyPublishRequest? Last { get; private set; }

    public Task<Result<KiwifyPublishOutcome>> PublishAsync(KiwifyPublishRequest request, CancellationToken ct)
    {
        Count++;
        Last = request;
        var outcome = new KiwifyPublishOutcome(
            $"kw-{request.Slug}", $"https://pay.kiwify.com.br/{request.Slug}");
        return Task.FromResult(Result.Success(outcome));
    }
}
