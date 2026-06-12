using Ebook.Domain.Abstractions;
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

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<EbookDbContext>().Database.EnsureCreated();
        return provider;
    }
}
