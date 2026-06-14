using System.Collections.Concurrent;
using Ebook.Application.Social;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>Publisher social determinístico para testes: sempre sucesso, sem rede/Meta.</summary>
public sealed class FakeSocialPublisher : ISocialPublisher
{
    private int _count;
    public int Count => _count;
    public ConcurrentBag<SocialPublishRequest> Requests { get; } = [];

    public Task<Result<SocialPublishOutcome>> PublishAsync(SocialPublishRequest request, CancellationToken ct)
    {
        var n = Interlocked.Increment(ref _count);
        Requests.Add(request);
        return Task.FromResult(Result.Success(new SocialPublishOutcome($"ext-{n}")));
    }
}
