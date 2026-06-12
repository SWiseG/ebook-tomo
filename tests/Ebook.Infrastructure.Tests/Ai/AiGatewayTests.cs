using Ebook.Application.Ai;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Infrastructure.Ai;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Tests.Ai;

public class AiGatewayTests : IDisposable
{
    private readonly string _promptsRoot =
        Path.Combine(Path.GetTempPath(), "ebook-tests", Guid.NewGuid().ToString("N"));

    public AiGatewayTests()
    {
        Directory.CreateDirectory(Path.Combine(_promptsRoot, "dev"));
        File.WriteAllText(Path.Combine(_promptsRoot, "dev", "echo.md"), "Eco: {{text}}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_promptsRoot))
        {
            Directory.Delete(_promptsRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Simula o elo final (Claude CLI) com resposta canned — testes nunca chamam IA real.</summary>
    private sealed class FakeFinalResolver : IAiResolver
    {
        public int Invocations;

        public Task<Result<AiResponse>?> TryResolveAsync(AiResolveContext context, CancellationToken ct)
        {
            Interlocked.Increment(ref Invocations);
            return Task.FromResult<Result<AiResponse>?>(
                Result.Success(new AiResponse("resposta-gerada", AiProviderKind.ClaudeCli, false, 10)));
        }
    }

    private (ServiceProvider Provider, FakeFinalResolver Fake) BuildHost()
    {
        var fake = new FakeFinalResolver();
        var provider = TestHost.Build(s =>
        {
            s.Configure<AiOptions>(o => o.PromptsPath = _promptsRoot);
            s.AddSingleton<IPromptLibrary, PromptLibrary>();
            s.AddScoped<IAiResolver, AiCacheResolver>();
            s.AddScoped<IAiResolver>(_ => fake);
            s.AddScoped<IAiGateway, AiGateway>();
        });
        return (provider, fake);
    }

    private static AiRequest EchoRequest(string text) => new(
        Purpose: "dev.echo",
        PromptTemplate: "dev/echo",
        Variables: new Dictionary<string, string> { ["text"] = text });

    [Fact]
    public async Task Primeira_chamada_gera_e_segunda_vem_do_cache()
    {
        var (provider, fake) = BuildHost();
        using var _ = provider;

        using (var scope = provider.CreateScope())
        {
            var gateway = scope.ServiceProvider.GetRequiredService<IAiGateway>();
            var first = await gateway.CompleteAsync(EchoRequest("olá"));

            Assert.True(first.IsSuccess);
            Assert.Equal(AiProviderKind.ClaudeCli, first.Value.Provider);
            Assert.False(first.Value.CacheHit);
        }

        using (var scope = provider.CreateScope())
        {
            var gateway = scope.ServiceProvider.GetRequiredService<IAiGateway>();
            var second = await gateway.CompleteAsync(EchoRequest("olá"));

            Assert.True(second.IsSuccess);
            Assert.Equal(AiProviderKind.Cache, second.Value.Provider);
            Assert.True(second.Value.CacheHit);
            Assert.Equal("resposta-gerada", second.Value.Content);
        }

        Assert.Equal(1, fake.Invocations); // o provedor caro só foi acionado uma vez

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(2, await db.AiUsages.CountAsync());
        Assert.Equal(1, await db.AiUsages.CountAsync(u => u.CacheHit));
        var cache = await db.AiCache.SingleAsync();
        Assert.Equal(1, cache.HitCount);
    }

    [Fact]
    public async Task Inputs_diferentes_nao_compartilham_cache()
    {
        var (provider, fake) = BuildHost();
        using var _ = provider;
        using var scope = provider.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAiGateway>();

        await gateway.CompleteAsync(EchoRequest("a"));
        await gateway.CompleteAsync(EchoRequest("b"));

        Assert.Equal(2, fake.Invocations);
    }

    [Fact]
    public async Task Template_inexistente_falha_sem_chamar_provedores()
    {
        var (provider, fake) = BuildHost();
        using var _ = provider;
        using var scope = provider.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAiGateway>();

        var result = await gateway.CompleteAsync(new AiRequest(
            "x", "nao/existe", new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.TemplateNotFound", result.Error.Code);
        Assert.Equal(0, fake.Invocations);
    }

    [Fact]
    public async Task Orcamento_mensal_excedido_aborta_a_cadeia()
    {
        var provider = TestHost.Build(s =>
        {
            s.Configure<AiOptions>(o =>
            {
                o.PromptsPath = _promptsRoot;
                o.DefaultMonthlyCallCap = 0; // teto zero: nenhuma chamada permitida
            });
            s.AddSingleton<IPromptLibrary, PromptLibrary>();
            s.AddSingleton<ClaudeCliClient>();
            s.AddScoped<Ebook.Application.Common.Settings.ISettingsStore, Ebook.Infrastructure.Settings.SettingsStore>();
            s.AddScoped<IAiResolver, ClaudeCliResolver>();
            s.AddScoped<IAiGateway, AiGateway>();
        });
        using var host = provider;
        using var scope = provider.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAiGateway>();

        var result = await gateway.CompleteAsync(EchoRequest("olá"));

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.BudgetExceeded", result.Error.Code);
    }
}
