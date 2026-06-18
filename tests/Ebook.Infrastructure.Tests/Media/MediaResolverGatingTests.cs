using Ebook.Application.Media;
using Ebook.Infrastructure.Media;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Tests.Media;

/// <summary>E14 Inc. 2 — provedores com chave: gating (sem rede). Desligados sem credencial.</summary>
public class MediaResolverGatingTests
{
    private static IOptions<MediaOptions> Opt(MediaOptions o) => Options.Create(o);

    [Fact]
    public void Gemini_desligado_sem_chave_ligado_com_chave()
    {
        using var http = new HttpClient();

        var off = new GeminiImageResolver(http, Opt(new MediaOptions { Gemini = { Enabled = true } }));
        Assert.False(off.Enabled);

        var on = new GeminiImageResolver(http, Opt(new MediaOptions
        {
            Gemini = { Enabled = true, ApiKey = "k", DailyLimit = 50 },
        }));
        Assert.True(on.Enabled);
        Assert.Equal(MediaProvider.Gemini, on.Provider);
        Assert.Equal(50, on.DailyLimit);
    }

    [Fact]
    public void Cloudflare_exige_account_e_token()
    {
        using var http = new HttpClient();

        var semConta = new CloudflareImageResolver(http, Opt(new MediaOptions { Cloudflare = { Enabled = true, ApiKey = "k" } }));
        Assert.False(semConta.Enabled);

        var completo = new CloudflareImageResolver(http, Opt(new MediaOptions
        {
            Cloudflare = { Enabled = true, ApiKey = "k", AccountId = "acc" },
        }));
        Assert.True(completo.Enabled);
        Assert.Equal(MediaProvider.Cloudflare, completo.Provider);
    }

    [Fact]
    public void HuggingFace_desligado_sem_chave()
    {
        using var http = new HttpClient();
        Assert.False(new HuggingFaceImageResolver(http, Opt(new MediaOptions { HuggingFace = { Enabled = true } })).Enabled);
        Assert.True(new HuggingFaceImageResolver(http, Opt(new MediaOptions { HuggingFace = { Enabled = true, ApiKey = "k" } })).Enabled);
    }

    [Fact]
    public void Pollinations_ligado_por_padrao_sem_chave()
    {
        using var http = new HttpClient();
        Assert.True(new PollinationsMediaResolver(http, Opt(new MediaOptions())).Enabled);
    }
}
