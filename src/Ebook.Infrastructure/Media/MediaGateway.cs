using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Ebook.Application.Media;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// Gateway de mídia (E14): percorre os provedores na ordem de registro (free-first), respeitando
/// cota diária, e cacheia toda imagem nova de forma content-addressable. Espelha o AiGateway de texto.
/// Nunca propaga exceção de provedor — registra e tenta o próximo. Falha tipada se nenhum atende.
/// </summary>
public sealed class MediaGateway(
    IEnumerable<IMediaResolver> resolvers,
    EbookDbContext db,
    IArtifactStore artifactStore,
    IClock clock,
    ILogger<MediaGateway> logger) : IMediaGateway
{
    public async Task<Result<MediaResult>> GenerateAsync(MediaBrief brief, CancellationToken ct = default)
    {
        var hash = Hash(brief);

        // 1. cache content-addressable (custo zero)
        var cached = await db.MediaCache.FirstOrDefaultAsync(c => c.Hash == hash, ct);
        if (cached is not null)
        {
            var bytes = await artifactStore.ReadBytesAsync(cached.Path, ct);
            if (bytes is not null)
            {
                cached.HitCount++;
                cached.LastHitAtUtc = clock.UtcNow;
                await RecordUsageAsync(brief.Purpose, MediaProvider.Cache, cacheHit: true, bytes.Length, 0, ct);
                return Result.Success(new MediaResult(bytes, MediaProvider.Cache, CacheHit: true));
            }

            db.MediaCache.Remove(cached); // índice órfão: arquivo sumiu
            await db.SaveChangesAsync(ct);
        }

        // 2. cadeia de provedores (na ordem de registro)
        foreach (var resolver in resolvers)
        {
            if (!resolver.Enabled || await OverQuotaAsync(resolver, ct))
            {
                continue;
            }

            byte[]? bytes;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                bytes = await resolver.TryGenerateAsync(brief, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Provedor de mídia {Provider} falhou; tentando o próximo", resolver.Provider);
                continue;
            }

            if (bytes is null || bytes.Length == 0)
            {
                continue;
            }

            var path = $"cache/media/{hash[..2]}/{hash}.img";
            await artifactStore.WriteBytesAsync(path, bytes, ct);
            db.MediaCache.Add(new MediaCacheRecord
            {
                Hash = hash,
                Purpose = brief.Purpose,
                Provider = resolver.Provider.ToString(),
                Path = path,
                CreatedAtUtc = clock.UtcNow,
            });
            await RecordUsageAsync(brief.Purpose, resolver.Provider, cacheHit: false, bytes.Length, (int)stopwatch.ElapsedMilliseconds, ct);

            logger.LogInformation("Imagem gerada por {Provider} ({Bytes} bytes) para {Purpose}",
                resolver.Provider, bytes.Length, brief.Purpose);
            return Result.Success(new MediaResult(bytes, resolver.Provider, CacheHit: false));
        }

        return Result.Failure<MediaResult>(MediaErrors.NoProvider);
    }

    private async Task<bool> OverQuotaAsync(IMediaResolver resolver, CancellationToken ct)
    {
        if (resolver.DailyLimit <= 0)
        {
            return false;
        }

        var dayStart = clock.UtcNow.Date;
        var provider = resolver.Provider.ToString();
        var usedToday = await db.MediaUsages
            .CountAsync(u => u.Provider == provider && !u.CacheHit && u.CreatedAtUtc >= dayStart, ct);
        return usedToday >= resolver.DailyLimit;
    }

    private async Task RecordUsageAsync(
        string purpose, MediaProvider provider, bool cacheHit, int bytes, int durationMs, CancellationToken ct)
    {
        db.MediaUsages.Add(new MediaUsageRecord
        {
            Id = Guid.NewGuid(),
            Purpose = purpose,
            Provider = provider.ToString(),
            CacheHit = cacheHit,
            Bytes = bytes,
            DurationMs = durationMs,
            CreatedAtUtc = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string Hash(MediaBrief b) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{b.Purpose}\n{b.Prompt}\n{b.Width}x{b.Height}")));
}
