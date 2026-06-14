using Ebook.Application.Content.Images;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>Image composer fake (sem Skia): devolve um PNG mínimo e conta as composições.</summary>
public sealed class FakeImageComposer : IImageComposer
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    public int CoverCount { get; private set; }
    public int MockupCount { get; private set; }
    public int SocialCount { get; private set; }

    public byte[] RenderCover(CoverArt art, byte[]? backgroundPhoto = null)
    {
        CoverCount++;
        return [.. Png];
    }

    public byte[] RenderMockup(byte[] coverPng, NichePalette palette)
    {
        MockupCount++;
        return [.. Png];
    }

    public byte[] RenderSocial(SocialArt art, byte[]? backgroundPhoto = null)
    {
        SocialCount++;
        return [.. Png];
    }
}

/// <summary>Photo provider fake: sem foto (composer cai no gradiente da paleta).</summary>
public sealed class NullPhotoProvider : IPhotoProvider
{
    public Task<byte[]?> TryGetBackgroundAsync(string query, CancellationToken ct = default) =>
        Task.FromResult<byte[]?>(null);
}
