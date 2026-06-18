using Ebook.Application.Media;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>Resolver de mídia falso (sem rede): devolve bytes fixos (ou null) e conta as chamadas.</summary>
public sealed class FakeMediaResolver(MediaProvider provider, byte[]? result, bool enabled = true, int dailyLimit = 0)
    : IMediaResolver
{
    public int Calls { get; private set; }

    public MediaProvider Provider => provider;
    public bool Enabled => enabled;
    public int DailyLimit => dailyLimit;

    public Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(result);
    }
}
