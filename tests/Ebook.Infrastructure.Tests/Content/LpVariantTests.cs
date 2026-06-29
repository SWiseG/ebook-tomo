using System.Text.Json;
using Ebook.Application;
using Ebook.Application.Analytics;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Settings;
using Ebook.Application.Content;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Analytics;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Ebook.Infrastructure.Analytics;
using Ebook.Infrastructure.Content;
using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Settings;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ebook.Infrastructure.Tests.Content;

/// <summary>C1 — variantes de LP: LpJobHandler gera N LpVariant com arquivos HTML distintos.</summary>
public class LpVariantTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static ServiceProvider Build() => TestHost.Build(s =>
    {
        s.AddApplication();
        s.AddSingleton<IArtifactStore, FileArtifactStore>();
        s.AddScoped<ISettingsStore, SettingsStore>();
        s.AddScoped<INicheRepository, NicheRepository>();
        s.AddScoped<IProductRepository, ProductRepository>();
        s.AddScoped<IArtifactRepository, ArtifactRepository>();
        s.AddScoped<ILpVariantRepository, LpVariantRepository>();
        s.AddScoped<IAnalyticsRecorder, AnalyticsRecorder>();
        s.AddScoped<IMetricsAggregator, MetricsAggregator>();
        s.AddScoped<IMetricsReader, MetricsReader>();
        // Register LpJobHandler directly so tests can resolve it without enumerating all IJobHandlers
        s.AddScoped<LpJobHandler>();
    });

    private static async Task<(Guid NicheId, Guid ProductId, string Slug)> SeedAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();

        var niche = Niche.Discover("financas-pessoais", "Finanças Pessoais", 0.8, "{}", 1, DateTime.UtcNow);
        db.Niches.Add(niche);
        var product = Product.Create(niche.Id, "guia-financas", "Guia de Finanças", QualityTier.Commercial, DateTime.UtcNow);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // seed minimal LP copy JSON so LpJobHandler can render the LP
        var copy = new
        {
            headline = "Domine suas finanças em 30 dias",
            subheadline = "O guia prático para quem quer sair do vermelho",
            bullets = new[] { "Controle total do orçamento", "Reserva de emergência em 3 meses" },
            painSection = "Você chega ao fim do mês sem saber para onde foi o dinheiro. Isso precisa mudar.",
            solutionSection = "Com este guia você terá um sistema simples para controlar cada centavo.",
            price = new { anchor = 97, current = 47 },
            finalCta = new { headline = "Transforme sua relação com o dinheiro hoje", button = "Quero começar agora" },
        };
        await fileStore.WriteTextAsync(
            ContentPaths.SalesCopy(product.Slug),
            JsonSerializer.Serialize(copy, JsonOpts));

        return (niche.Id, product.Id, product.Slug);
    }

    private static async Task SetVariantCountAsync(ServiceProvider provider, int count)
    {
        using var scope = provider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        await settings.SetAsync(SettingKeys.LpVariantCount, count);
    }

    private static async Task<Result> RunLpJobAsync(ServiceProvider provider, Guid productId)
    {
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<LpJobHandler>();
        return await handler.ExecuteAsync(
            JsonSerializer.Serialize(new LpJobPayload(productId), JsonOpts),
            CancellationToken.None);
    }

    [Fact]
    public async Task VariantCount2_gera_2_registros_e_2_arquivos_distintos()
    {
        using var provider = Build();
        var (_, productId, slug) = await SeedAsync(provider);
        await SetVariantCountAsync(provider, 2);

        var result = await RunLpJobAsync(provider, productId);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : "ok");

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var artifacts = scope.ServiceProvider.GetRequiredService<IArtifactStore>();

        var variants = await db.LpVariants.AsNoTracking()
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.VariantTag)
            .ToListAsync();

        Assert.Equal(2, variants.Count);
        Assert.Equal("v1", variants[0].VariantTag);
        Assert.Equal("v2", variants[1].VariantTag);

        // arquivos são distintos e existem no artifact store
        Assert.NotEqual(variants[0].FilePath, variants[1].FilePath);
        Assert.NotNull(await artifacts.ReadBytesAsync(variants[0].FilePath));
        Assert.NotNull(await artifacts.ReadBytesAsync(variants[1].FilePath));

        // os HTMLs têm CTAs diferentes (v1 = "Quero agora", v2 = "Baixar agora")
        var html1 = System.Text.Encoding.UTF8.GetString((await artifacts.ReadBytesAsync(variants[0].FilePath))!);
        var html2 = System.Text.Encoding.UTF8.GetString((await artifacts.ReadBytesAsync(variants[1].FilePath))!);
        Assert.Contains("Quero agora", html1, StringComparison.Ordinal);
        Assert.Contains("Baixar agora", html2, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VariantCount1_nao_regressao_gera_1_registro()
    {
        using var provider = Build();
        var (_, productId, _) = await SeedAsync(provider);
        await SetVariantCountAsync(provider, 1);

        var result = await RunLpJobAsync(provider, productId);
        Assert.True(result.IsSuccess);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var count = await db.LpVariants.AsNoTracking().CountAsync(v => v.ProductId == productId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Pixel_com_variantTag_grava_AnalyticsEvent_com_tag()
    {
        using var provider = Build();
        var (_, productId, slug) = await SeedAsync(provider);

        using var scope = provider.CreateScope();
        var recorder = scope.ServiceProvider.GetRequiredService<IAnalyticsRecorder>();
        await recorder.RecordAsync(
            new AnalyticsHit(slug, AnalyticsEventType.Visit, null, null, null, VariantTag: "v2"));

        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var ev = await db.AnalyticsEvents.AsNoTracking()
            .SingleAsync(e => e.ProductId == productId);

        Assert.Equal("v2", ev.VariantTag);
    }

    [Fact]
    public async Task Pixel_sem_variantTag_grava_AnalyticsEvent_sem_tag()
    {
        using var provider = Build();
        var (_, productId, slug) = await SeedAsync(provider);

        using var scope = provider.CreateScope();
        var recorder = scope.ServiceProvider.GetRequiredService<IAnalyticsRecorder>();
        await recorder.RecordAsync(
            new AnalyticsHit(slug, AnalyticsEventType.Visit, "instagram", null, null));

        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var ev = await db.AnalyticsEvents.AsNoTracking()
            .SingleAsync(e => e.ProductId == productId);

        Assert.Null(ev.VariantTag);
    }

    [Fact]
    public async Task Reexecutar_job_e_idempotente_nao_duplica_variantes()
    {
        using var provider = Build();
        var (_, productId, _) = await SeedAsync(provider);
        await SetVariantCountAsync(provider, 2);

        await RunLpJobAsync(provider, productId);
        await RunLpJobAsync(provider, productId); // segunda passada

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        var count = await db.LpVariants.AsNoTracking().CountAsync(v => v.ProductId == productId);
        Assert.Equal(2, count); // não duplicou
    }
}
