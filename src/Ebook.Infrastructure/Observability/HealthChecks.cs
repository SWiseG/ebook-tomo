using Ebook.Infrastructure.FileStore;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Observability;

public sealed class DatabaseHealthCheck(EbookDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy("SQLite acessível")
                : HealthCheckResult.Unhealthy("SQLite inacessível");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQLite inacessível", ex);
        }
    }
}

public sealed class DiskSpaceHealthCheck(IOptions<DataOptions> options) : IHealthCheck
{
    private const long MinFreeBytes = 1L * 1024 * 1024 * 1024; // 1 GB

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(root);
        var drive = new DriveInfo(Path.GetPathRoot(root)!);
        var free = drive.AvailableFreeSpace;

        var result = free >= MinFreeBytes
            ? HealthCheckResult.Healthy($"{free / (1024 * 1024)} MB livres")
            : HealthCheckResult.Unhealthy($"Apenas {free / (1024 * 1024)} MB livres (mínimo 1 GB)");
        return Task.FromResult(result);
    }
}

public sealed class DeadJobsHealthCheck(EbookDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var deadRecent = await db.Jobs.CountAsync(
            j => j.Status == JobStatus.Dead && j.FinishedAtUtc >= cutoff, ct);

        return deadRecent == 0
            ? HealthCheckResult.Healthy("Sem jobs em dead-letter nas últimas 24h")
            : HealthCheckResult.Degraded($"{deadRecent} job(s) em dead-letter nas últimas 24h");
    }
}
