using Ebook.Application.Common.Settings;
using Ebook.Application.Discovery;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Niches;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Settings;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Infrastructure.Tests.Discovery;

public class TrendDiscoveryTests
{
    private static ServiceProvider Build() => TestHost.Build(s =>
    {
        s.AddScoped<ISettingsStore, SettingsStore>();
        s.AddScoped<INicheRepository, NicheRepository>();
        s.AddScoped<ITrendSnapshotRepository, TrendSnapshotRepository>();
    });

    private static DiscoverNichesJobHandler BuildHandler(IServiceProvider scope) => new(
        [
            new FakeTrendSource(TrendSource.Reddit,
                new TrendSignal("Emagrecimento", 0.9, 0.2, 0.8),
                new TrendSignal("Investir na Bolsa", 0.6, 0.7, 0.9)),
            new FakeTrendSource(TrendSource.Autocomplete,
                new TrendSignal("Emagrecimento", 0.8, 0.3, 0.7),
                new TrendSignal("Produtividade", 0.5, 0.5, 0.4)),
            new ThrowingTrendSource() // degradação graciosa
        ],
        scope.GetRequiredService<INicheRepository>(),
        scope.GetRequiredService<ITrendSnapshotRepository>(),
        scope.GetRequiredService<ISettingsStore>(),
        scope.GetRequiredService<IFileStore>(),
        scope.GetRequiredService<IUnitOfWork>(),
        scope.GetRequiredService<IClock>(),
        NullLogger<DiscoverNichesJobHandler>.Instance);

    [Fact]
    public async Task Discover_cria_nichos_pontuados_ranqueados_e_emite_eventos()
    {
        using var provider = Build();

        using (var scope = provider.CreateScope())
        {
            var result = await BuildHandler(scope.ServiceProvider).ExecuteAsync("{}", CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var fileStore = verify.ServiceProvider.GetRequiredService<IFileStore>();

        var niches = await db.Niches.AsNoTracking().OrderByDescending(n => n.Score).ToListAsync();
        Assert.Equal(3, niches.Count);
        Assert.All(niches, n => Assert.Equal(NicheStatus.Candidate, n.Status));

        // emagrecimento (alto volume, baixa concorrência, presente em 2 fontes) deve liderar
        Assert.Equal("emagrecimento", niches[0].Slug);
        Assert.True(niches[0].Score > niches[1].Score);

        // E02-06: NicheDiscovered emitido para cada nicho (via Outbox)
        Assert.Equal(3, await db.OutboxEvents.CountAsync(e => e.Type == nameof(NicheDiscovered)));

        // evidências: emagrecimento tem 2 snapshots (Reddit + Autocomplete), e o payload existe no FileStore
        var emagrecimento = niches.Single(n => n.Slug == "emagrecimento");
        var snapshots = await db.TrendSnapshots.AsNoTracking().Where(t => t.NicheId == emagrecimento.Id).ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.NotNull(await fileStore.ReadTextAsync(snapshots[0].PayloadPath));
    }

    [Fact]
    public async Task Discover_e_idempotente_por_slug()
    {
        using var provider = Build();

        using (var scope = provider.CreateScope())
        {
            await BuildHandler(scope.ServiceProvider).ExecuteAsync("{}", CancellationToken.None);
        }

        using (var scope = provider.CreateScope())
        {
            await BuildHandler(scope.ServiceProvider).ExecuteAsync("{}", CancellationToken.None);
        }

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        Assert.Equal(3, await db.Niches.CountAsync()); // segunda passada não duplica
        Assert.Equal(4, await db.TrendSnapshots.CountAsync()); // 2 + 1 + 1
    }

    [Fact]
    public async Task Aprovar_nicho_muda_status_para_selected()
    {
        using var provider = Build();
        Guid nicheId;

        using (var scope = provider.CreateScope())
        {
            var niche = Niche.Discover("financas", "Finanças", 0.7, "{}", 6, DateTime.UtcNow);
            scope.ServiceProvider.GetRequiredService<INicheRepository>().Add(niche);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            nicheId = niche.Id;
        }

        using (var scope = provider.CreateScope())
        {
            var handler = new ApproveNicheCommandHandler(
                scope.ServiceProvider.GetRequiredService<INicheRepository>(),
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
            var result = await handler.HandleAsync(new ApproveNicheCommand(nicheId), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EbookDbContext>();
        var saved = await db.Niches.AsNoTracking().SingleAsync(n => n.Id == nicheId);
        Assert.Equal(NicheStatus.Selected, saved.Status);
    }
}
