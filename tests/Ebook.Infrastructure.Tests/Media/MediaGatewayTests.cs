using Ebook.Application.Media;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ebook.Infrastructure.Tests.Media;

/// <summary>E14 — Media Gateway: cadeia free-first, cache content-addressable, telemetria e cota diária.</summary>
public class MediaGatewayTests
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[300]];

    private static ServiceProvider Build(params IMediaResolver[] resolvers) => TestHost.Build(s =>
    {
        s.AddSingleton<IArtifactStore, FileArtifactStore>();
        foreach (var r in resolvers)
        {
            s.AddSingleton(r);
        }

        s.AddScoped<IMediaGateway, Ebook.Infrastructure.Media.MediaGateway>();
    });

    private static MediaBrief Brief(string purpose = "background") =>
        new(purpose, $"prompt para {purpose}", "consulta", "nicho", 512, 512);

    private static async Task<Result<MediaResult>> GenerateAsync(ServiceProvider provider, MediaBrief brief)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediaGateway>().GenerateAsync(brief);
    }

    [Fact]
    public async Task Usa_o_primeiro_provedor_que_entrega()
    {
        var gemini = new FakeMediaResolver(MediaProvider.Gemini, result: null);     // não atende
        var pollinations = new FakeMediaResolver(MediaProvider.Pollinations, Png);  // atende
        using var provider = Build(gemini, pollinations);

        var result = await GenerateAsync(provider, Brief());

        Assert.True(result.IsSuccess);
        Assert.Equal(MediaProvider.Pollinations, result.Value.Provider);
        Assert.False(result.Value.CacheHit);
        Assert.Equal(1, gemini.Calls);
        Assert.Equal(1, pollinations.Calls);
    }

    [Fact]
    public async Task Cacheia_e_nao_rechama_o_provedor()
    {
        var pollinations = new FakeMediaResolver(MediaProvider.Pollinations, Png);
        using var provider = Build(pollinations);

        var first = await GenerateAsync(provider, Brief());
        var second = await GenerateAsync(provider, Brief()); // mesmo brief → cache

        Assert.Equal(MediaProvider.Pollinations, first.Value.Provider);
        Assert.True(second.IsSuccess);
        Assert.Equal(MediaProvider.Cache, second.Value.Provider);
        Assert.True(second.Value.CacheHit);
        Assert.Equal(1, pollinations.Calls); // não chamou o provedor de novo
    }

    [Fact]
    public async Task Registra_telemetria_de_uso()
    {
        using var provider = Build(new FakeMediaResolver(MediaProvider.Pollinations, Png));

        await GenerateAsync(provider, Brief());

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var usage = await db.MediaUsages.AsNoTracking().SingleAsync();
        Assert.Equal(nameof(MediaProvider.Pollinations), usage.Provider);
        Assert.False(usage.CacheHit);
        Assert.True(usage.Bytes > 0);
    }

    [Fact]
    public async Task Respeita_a_cota_diaria()
    {
        var pollinations = new FakeMediaResolver(MediaProvider.Pollinations, Png, dailyLimit: 1);
        using var provider = Build(pollinations);

        var first = await GenerateAsync(provider, Brief("a"));   // consome a cota
        var second = await GenerateAsync(provider, Brief("b"));  // brief diferente (sem cache) → cota estourada

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(1, pollinations.Calls); // o 2º nem chamou o provedor
    }

    [Fact]
    public async Task Falha_tipada_quando_nenhum_provedor_atende()
    {
        using var provider = Build(new FakeMediaResolver(MediaProvider.Pollinations, result: null));

        var result = await GenerateAsync(provider, Brief());

        Assert.True(result.IsFailure);
        Assert.Equal(MediaErrors.NoProvider.Code, result.Error.Code);
    }
}
