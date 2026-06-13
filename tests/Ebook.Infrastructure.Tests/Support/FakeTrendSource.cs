using Ebook.Application.Discovery;
using Ebook.Domain.Niches;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>Fonte de tendência determinística para testes (sem rede).</summary>
public sealed class FakeTrendSource(TrendSource source, params TrendSignal[] signals) : ITrendSource
{
    public TrendSource Source => source;

    public Task<IReadOnlyList<TrendSignal>> CollectAsync(string category, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TrendSignal>>(signals);
}

/// <summary>Fonte que sempre falha — valida a degradação graciosa da descoberta.</summary>
public sealed class ThrowingTrendSource : ITrendSource
{
    public TrendSource Source => TrendSource.Amazon;

    public Task<IReadOnlyList<TrendSignal>> CollectAsync(string category, CancellationToken ct) =>
        throw new HttpRequestException("fonte indisponível");
}
