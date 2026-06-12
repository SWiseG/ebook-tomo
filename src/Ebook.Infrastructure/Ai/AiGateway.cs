using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Ebook.Application.Ai;
using Ebook.Application.Common.Settings;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Ai;

public sealed record AiResolveContext(AiRequest Request, string RenderedPrompt, string Hash);

/// <summary>
/// Um elo da cadeia de resolução. Retorna null quando não consegue atender
/// (próximo elo tenta); retorna Result de falha para abortar a cadeia.
/// </summary>
public interface IAiResolver
{
    Task<Result<AiResponse>?> TryResolveAsync(AiResolveContext context, CancellationToken ct);
}

/// <summary>Elo 1: cache exato content-addressable (hash de purpose + prompt). Custo zero.</summary>
public sealed class AiCacheResolver(EbookDbContext db, IFileStore fileStore, IClock clock) : IAiResolver
{
    public async Task<Result<AiResponse>?> TryResolveAsync(AiResolveContext context, CancellationToken ct)
    {
        var entry = await db.AiCache.FirstOrDefaultAsync(c => c.Hash == context.Hash, ct);
        if (entry is null)
        {
            return null;
        }

        var content = await fileStore.ReadTextAsync(entry.ResponsePath, ct);
        if (content is null)
        {
            db.AiCache.Remove(entry); // índice órfão: arquivo sumiu
            await db.SaveChangesAsync(ct);
            return null;
        }

        entry.HitCount++;
        entry.LastHitAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(new AiResponse(content, AiProviderKind.Cache, CacheHit: true, DurationMs: 0));
    }
}

/// <summary>
/// Elo final: Claude Code CLI com a assinatura Pro. Guarda de orçamento mensal
/// (Settings ai.monthlyCallCap) antes de consumir a assinatura.
/// </summary>
public sealed class ClaudeCliResolver(
    ClaudeCliClient client,
    EbookDbContext db,
    ISettingsStore settings,
    IOptions<AiOptions> options,
    IClock clock) : IAiResolver
{
    public async Task<Result<AiResponse>?> TryResolveAsync(AiResolveContext context, CancellationToken ct)
    {
        var cap = await settings.GetOrDefaultAsync(SettingKeys.AiMonthlyCallCap, options.Value.DefaultMonthlyCallCap, ct);
        var monthStart = new DateTime(clock.UtcNow.Year, clock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var callsThisMonth = await db.AiUsages
            .CountAsync(u => u.Provider == nameof(AiProviderKind.ClaudeCli) && u.CreatedAtUtc >= monthStart, ct);

        if (callsThisMonth >= cap)
        {
            return Result.Failure<AiResponse>(AiErrors.BudgetExceeded);
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await client.CompleteAsync(context.RenderedPrompt, ct);
        if (result.IsFailure)
        {
            return Result.Failure<AiResponse>(result.Error);
        }

        return Result.Success(new AiResponse(
            result.Value, AiProviderKind.ClaudeCli, CacheHit: false, (int)stopwatch.ElapsedMilliseconds));
    }
}

/// <summary>
/// Único ponto de acesso a IA. Percorre a cadeia de resolvers na ordem de registro,
/// registra telemetria (AiUsage) e alimenta o cache com toda resposta nova.
/// </summary>
public sealed class AiGateway(
    IPromptLibrary promptLibrary,
    IEnumerable<IAiResolver> resolvers,
    EbookDbContext db,
    IFileStore fileStore,
    IClock clock,
    ILogger<AiGateway> logger) : IAiGateway
{
    public async Task<Result<AiResponse>> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        var rendered = await promptLibrary.RenderAsync(request.PromptTemplate, request.Variables, ct);
        if (rendered.IsFailure)
        {
            return Result.Failure<AiResponse>(rendered.Error);
        }

        var hash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Purpose + "\n" + rendered.Value)));
        var context = new AiResolveContext(request, rendered.Value, hash);

        foreach (var resolver in resolvers)
        {
            var outcome = await resolver.TryResolveAsync(context, ct);
            if (outcome is null)
            {
                continue;
            }

            if (outcome.IsFailure)
            {
                logger.LogWarning("AI Gateway abortou em {Resolver} para {Purpose}: {Error}",
                    resolver.GetType().Name, request.Purpose, outcome.Error);
                return outcome;
            }

            await RecordAsync(context, outcome.Value, ct);
            return outcome;
        }

        return Result.Failure<AiResponse>(new Error("Ai.NoProvider",
            $"Nenhum provedor de IA conseguiu atender o purpose '{request.Purpose}'."));
    }

    private async Task RecordAsync(AiResolveContext context, AiResponse response, CancellationToken ct)
    {
        db.AiUsages.Add(new AiUsageRecord
        {
            Id = Guid.NewGuid(),
            Purpose = context.Request.Purpose,
            ProductId = context.Request.ProductId,
            Provider = response.Provider.ToString(),
            CacheHit = response.CacheHit,
            InputTokensEst = context.RenderedPrompt.Length / 4,
            OutputTokensEst = response.Content.Length / 4,
            DurationMs = response.DurationMs,
            CreatedAtUtc = clock.UtcNow
        });

        if (!response.CacheHit)
        {
            var path = $"ai-cache/{context.Hash[..2]}/{context.Hash}.txt";
            await fileStore.WriteTextAsync(path, response.Content, ct);
            db.AiCache.Add(new AiCacheRecord
            {
                Hash = context.Hash,
                Purpose = context.Request.Purpose,
                ResponsePath = path,
                CreatedAtUtc = clock.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
