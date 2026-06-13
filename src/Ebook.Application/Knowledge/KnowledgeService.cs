using System.Security.Cryptography;
using System.Text;
using Ebook.Application.Ai;
using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Knowledge;

/// <summary>
/// Garante um KnowledgePack para um nicho. Reaproveita o pacote já indexado
/// (sem nova chamada de IA) ou gera um novo via AI Gateway e o indexa.
/// </summary>
public interface IKnowledgeService
{
    Task<Result<string>> EnsurePackAsync(Niche niche, QualityTier tier, CancellationToken ct = default);
}

public sealed class KnowledgeService(
    IAiGateway aiGateway,
    IKnowledgeRepository repository,
    IFileStore fileStore,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<KnowledgeService> logger) : IKnowledgeService
{
    public async Task<Result<string>> EnsurePackAsync(Niche niche, QualityTier tier, CancellationToken ct = default)
    {
        var existing = await repository.GetPackByNicheAsync(niche.Id, ct);
        if (existing is not null)
        {
            var cached = await fileStore.ReadTextAsync(existing.Path, ct);
            if (cached is not null)
            {
                existing.MarkReused();
                await unitOfWork.SaveChangesAsync(ct);
                logger.LogInformation("KnowledgePack do nicho {Slug} reaproveitado (reuso #{Count})",
                    niche.Slug, existing.ReuseCount);
                return Result.Success(cached);
            }

            logger.LogWarning("Índice de KnowledgePack do nicho {Slug} órfão (arquivo ausente); regerando", niche.Slug);
        }

        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "knowledge.pack",
            PromptTemplate: "knowledge/pack",
            Variables: new Dictionary<string, string>
            {
                ["niche"] = niche.Name,
                ["topic"] = niche.Name,
                ["language"] = "pt-BR"
            },
            Tier: tier,
            MaxOutputTokensEst: 2500), ct);

        if (ai.IsFailure)
        {
            return Result.Failure<string>(ai.Error);
        }

        var content = ai.Value.Content;
        var parsed = AiJson.Parse<KnowledgePackDto>(content, "knowledge.pack");
        if (parsed.IsFailure)
        {
            return Result.Failure<string>(parsed.Error);
        }

        var hash = Hash(content);
        var path = $"niches/{niche.Slug}/knowledge/packs/{hash}.knowledge.json";
        await fileStore.WriteTextAsync(path, content, ct);

        var topic = string.IsNullOrWhiteSpace(parsed.Value.Topic) ? niche.Name : parsed.Value.Topic;
        var keywords = parsed.Value.Audience?.Vocabulary is { Count: > 0 } vocab
            ? string.Join(',', vocab)
            : string.Empty;

        repository.Add(KnowledgeAsset.Create(
            niche.Id, KnowledgeAssetType.KnowledgePack, topic, keywords, path, hash, clock.UtcNow));
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("KnowledgePack gerado para o nicho {Slug} ({Provider})", niche.Slug, ai.Value.Provider);
        return Result.Success(content);
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
