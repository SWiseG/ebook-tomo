using Ebook.Application.Ai;
using Ebook.Application.Content;
using Ebook.Application.Knowledge;
using Ebook.Application.Media;
using Ebook.Application.Publishing;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Social;
using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// Provider de testes: SQLite in-memory (conexão única mantida aberta),
/// FileStore em diretório temporário e loggers nulos.
/// </summary>
public static class TestHost
{
    public static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        services.AddSingleton(connection); // mantém o banco vivo enquanto o provider existir
        services.AddDbContext<EbookDbContext>(o => o.UseSqlite(connection));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.Configure<DataOptions>(o =>
            o.RootPath = Path.Combine(Path.GetTempPath(), "ebook-tests", Guid.NewGuid().ToString("N")));
        services.AddSingleton<IFileStore, JsonFileStore>();

        // Media Gateway, PromptLibrary e EPUB sem rede (handlers de conteúdo precisam dos três)
        services.AddSingleton<IMediaGateway, FakeMediaGateway>();
        services.AddSingleton<IPromptLibrary, NullPromptLibrary>();
        services.AddSingleton<IEbookExporter, FakeEbookExporter>();
        services.AddSingleton<IStyleAnalyzer, FakeStyleAnalyzer>();
        services.AddSingleton<Application.Content.Images.ICoverQa, FakeCoverQa>();

        // Style analyzer sem CLI/visão: o JobWorker resolve todos os IJobHandler (StyleLearnJobHandler) ao processar
        services.AddSingleton<Application.Knowledge.IStyleAnalyzer, FakeStyleAnalyzer>();

        // Catálogo Kiwify falso (sem rede): a sincronização usa-o em vez da API real.
        services.AddSingleton<FakeKiwifyCatalog>();
        services.AddSingleton<IKiwifyCatalog>(sp => sp.GetRequiredService<FakeKiwifyCatalog>());
        services.AddScoped<IChannelRepository, Infrastructure.Persistence.ChannelRepository>();

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<EbookDbContext>().Database.EnsureCreated();
        return provider;
    }
}
