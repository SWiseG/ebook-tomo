using Ebook.Application.Ai;
using Ebook.Application.Common.Text;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content.Images;

/// <summary>Um benefício planejado pela IA para a capa (texto + ícone) — docs/14 WP-4.</summary>
public sealed record CoverFeatureDto(string Text, string Icon);

/// <summary>
/// Plano de capa gerado pela IA: os elementos de venda (eyebrow, subtítulo refinado, benefícios,
/// selo) + a descrição da CENA da ilustração de fundo + o layout. Alimenta tanto a composição Skia
/// (WP-6, fallback) quanto o prompt do caminho full-AI (WP-5).
/// </summary>
public sealed record CoverPlanDto(
    string Eyebrow,
    string Subtitle,
    IReadOnlyList<CoverFeatureDto> Features,
    string Seal,
    string Scene,
    string Layout);

/// <summary>
/// Diretor de capa por IA (docs/14 WP-4) — "toda revisão passa pela IA". Best-effort: devolve null
/// quando a IA está indisponível ou a saída é inválida; o chamador então usa só título/subtítulo.
/// </summary>
public interface ICoverDirector
{
    Task<CoverPlanDto?> PlanAsync(string title, string? subtitle, string nicheSlug, string topics, CancellationToken ct = default);
}

public sealed class CoverDirector(IAiGateway aiGateway, ILogger<CoverDirector> logger) : ICoverDirector
{
    public async Task<CoverPlanDto?> PlanAsync(string title, string? subtitle, string nicheSlug, string topics, CancellationToken ct = default)
    {
        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "cover.plan",
            PromptTemplate: "media/cover-plan",
            Variables: new Dictionary<string, string>
            {
                ["title"] = title,
                ["subtitle"] = string.IsNullOrWhiteSpace(subtitle) ? "(sem subtítulo)" : subtitle,
                ["niche"] = nicheSlug,
                ["topics"] = string.IsNullOrWhiteSpace(topics) ? title : topics,
            },
            MaxOutputTokensEst: 600), ct);

        if (ai.IsFailure)
        {
            logger.LogInformation("Plano de capa por IA indisponível para '{Title}' ({Error}).", title, ai.Error.Code);
            return null;
        }

        var parsed = AiJson.Parse<CoverPlanDto>(ai.Value.Content, "cover.plan");
        if (parsed.IsFailure || parsed.Value.Features is not { Count: > 0 })
        {
            logger.LogWarning("Plano de capa por IA inválido/vazio para '{Title}'.", title);
            return null;
        }

        // Normaliza: descarta benefícios sem texto e garante um ícone padrão.
        var features = parsed.Value.Features
            .Where(f => !string.IsNullOrWhiteSpace(f.Text))
            .Select(f => new CoverFeatureDto(f.Text.Trim(), string.IsNullOrWhiteSpace(f.Icon) ? "check" : f.Icon.Trim()))
            .ToList();

        return features.Count == 0 ? null : parsed.Value with { Features = features };
    }
}
