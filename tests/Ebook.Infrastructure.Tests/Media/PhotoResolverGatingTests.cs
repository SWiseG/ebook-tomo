using Ebook.Application.Media;
using Ebook.Infrastructure.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Tests.Media;

// Fase 2: os bancos de foto novos são gated por chave — sem chave, desligados e nunca tocam a rede.
public class PhotoResolverGatingTests
{
    private static readonly MediaBrief Brief =
        new("chapter-illustration", "prompt", "marketing digital", "marketing-digital", 800, 400);

    [Fact]
    public async Task Unsplash_sem_chave_fica_desligado_e_nao_gera()
    {
        var resolver = new UnsplashMediaResolver(
            new HttpClient(new ThrowingHandler()),
            Options.Create(new UnsplashOptions()),
            NullLogger<UnsplashMediaResolver>.Instance);

        Assert.False(resolver.Enabled);
        Assert.Null(await resolver.TryGenerateAsync(Brief, default));
    }

    [Fact]
    public async Task Pixabay_sem_chave_fica_desligado_e_nao_gera()
    {
        var resolver = new PixabayMediaResolver(
            new HttpClient(new ThrowingHandler()),
            Options.Create(new PixabayOptions()),
            NullLogger<PixabayMediaResolver>.Instance);

        Assert.False(resolver.Enabled);
        Assert.Null(await resolver.TryGenerateAsync(Brief, default));
    }

    [Fact]
    public void Com_chave_ficam_habilitados()
    {
        var unsplash = new UnsplashMediaResolver(
            new HttpClient(new ThrowingHandler()),
            Options.Create(new UnsplashOptions { AccessKey = "k" }),
            NullLogger<UnsplashMediaResolver>.Instance);
        var pixabay = new PixabayMediaResolver(
            new HttpClient(new ThrowingHandler()),
            Options.Create(new PixabayOptions { ApiKey = "k" }),
            NullLogger<PixabayMediaResolver>.Instance);

        Assert.True(unsplash.Enabled);
        Assert.True(pixabay.Enabled);
    }

    // garante que o caminho desligado não chega a fazer requisição HTTP
    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("não deveria tocar a rede sem chave");
    }
}
