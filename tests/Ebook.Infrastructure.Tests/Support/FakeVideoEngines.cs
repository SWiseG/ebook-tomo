using Ebook.Application.Video;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>TTS fake: devolve bytes mínimos de "áudio" sem Piper/rede.</summary>
public sealed class FakeTtsEngine : ITtsEngine
{
    public int Count { get; private set; }
    public string? LastText { get; private set; }

    public Task<Result<byte[]>> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        Count++;
        LastText = text;
        return Task.FromResult(Result.Success(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 1, 2, 3, 4 }));
    }
}

/// <summary>Compositor de vídeo fake: devolve bytes mínimos de "MP4" e conta cenas.</summary>
public sealed class FakeVideoComposer : IVideoComposer
{
    public int Count { get; private set; }
    public int LastSceneCount { get; private set; }

    public Task<Result<byte[]>> RenderAsync(VideoSpec spec, CancellationToken ct = default)
    {
        Count++;
        LastSceneCount = spec.Scenes.Count;
        // assinatura mínima de MP4 (ftyp box)
        var mp4 = new byte[] { 0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m' };
        return Task.FromResult(Result.Success(mp4));
    }
}
