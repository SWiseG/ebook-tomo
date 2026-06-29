using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content.Images;

/// <summary>
/// Direção de ARTE de imagens do produto (docs/15 Frente A): estilo, mood e orientação de sujeito.
/// Gerada por IA uma vez e consumida por PDF, LP e capa — coerência visual entre os três artefatos.
/// </summary>
public sealed record ProductBrand(string Mood, string ImageStyle, string SubjectGuidance)
{
    /// <summary>Acopla a direção de arte a um brief de imagem (cena base + estilo/mood/sujeito).</summary>
    public string Decorate(string basePrompt) =>
        $"{basePrompt.TrimEnd('.')}. Visual style: {ImageStyle}. Mood: {Mood}. Subjects: {SubjectGuidance}.";
}

/// <summary>Direção de arte padrão por categoria (piso determinístico quando não há IA).</summary>
public static class BrandCatalog
{
    public static ProductBrand For(NicheCategory category) => category switch
    {
        NicheCategory.Finance => new("confident, trustworthy, calm", "clean documentary photography, natural light", "real people managing money, tidy desks, notebooks, Brazilian context"),
        NicheCategory.Health => new("energetic, fresh, hopeful", "bright natural photography, airy", "active people, healthy food, outdoors, movement"),
        NicheCategory.SelfHelp => new("intimate, optimistic, reflective", "warm editorial photography, soft light", "real people in everyday transformation moments"),
        NicheCategory.Marketing => new("dynamic, bold, ambitious", "modern editorial photography, vivid", "entrepreneurs, screens, growth, contemporary workspace"),
        NicheCategory.Tech => new("futuristic, precise, innovative", "sleek modern editorial, cool tones", "developers, devices, clean interfaces, subtle data"),
        NicheCategory.Fiction => new("immersive, evocative, atmospheric", "cinematic illustration, dramatic light", "mood-driven scenes and characters"),
        NicheCategory.Education => new("clear, encouraging, focused", "clean editorial photography, natural light", "students studying, books, bright learning spaces"),
        _ => new("professional, aspirational", "premium editorial photography, natural light", "real people, meaningful objects, believable environments"),
    };
}

/// <summary>Lê a direção de arte do produto: brand.json (IA) → catálogo por categoria.</summary>
public interface IBrandResolver
{
    Task<ProductBrand> ResolveAsync(string? productSlug, string nicheSlug, CancellationToken ct = default);
}

public sealed class BrandResolver(IFileStore fileStore) : IBrandResolver
{
    public async Task<ProductBrand> ResolveAsync(string? productSlug, string nicheSlug, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(productSlug)
            && await fileStore.ReadTextAsync(ContentPaths.ProductBrand(productSlug), ct) is { Length: > 0 } json
            && AiJson.Parse<ProductBrand>(json, "brand") is { IsSuccess: true } p
            && !string.IsNullOrWhiteSpace(p.Value.ImageStyle))
        {
            return p.Value;
        }

        return BrandCatalog.For(NicheStyleCatalog.Classify(nicheSlug));
    }
}

/// <summary>Gera (IA) e persiste a direção de arte do produto. Idempotente; falha → catálogo.</summary>
public interface IBrandDirector
{
    Task EnsureAsync(string productSlug, string nicheSlug, Guid nicheId, string title, CancellationToken ct = default);
}

public sealed class BrandDirector(
    IAiGateway aiGateway,
    IFileStore fileStore,
    Knowledge.IStylePlaybookReader playbook,
    ILogger<BrandDirector> logger) : IBrandDirector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnsureAsync(string productSlug, string nicheSlug, Guid nicheId, string title, CancellationToken ct = default)
    {
        var path = ContentPaths.ProductBrand(productSlug);
        if (fileStore.Exists(path))
        {
            return;
        }

        var category = NicheStyleCatalog.Classify(nicheSlug);
        // docs/17 P3-12/14: realimenta a direção de arte com o estilo APRENDIDO do nicho (E15).
        var learned = await playbook.HintsAsync(nicheId, ct);

        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "ebook.brand",
            PromptTemplate: "ebook/brand",
            Variables: new Dictionary<string, string>
            {
                ["niche"] = nicheSlug, ["title"] = title, ["category"] = category.ToString(),
                ["learned"] = string.IsNullOrWhiteSpace(learned) ? "(nenhum)" : learned!,
            },
            MaxOutputTokensEst: 300), ct);

        if (ai.IsFailure)
        {
            // sem IA, mas com estilo aprendido: persiste o catálogo + dicas aprendidas.
            if (!string.IsNullOrWhiteSpace(learned))
            {
                var basef = BrandCatalog.For(category);
                await fileStore.WriteTextAsync(path,
                    JsonSerializer.Serialize(basef with { ImageStyle = $"{basef.ImageStyle}; {learned}" }, JsonOptions), ct);
            }

            return;
        }

        var parsed = AiJson.Parse<ProductBrand>(ai.Value.Content, "ebook.brand");
        if (parsed.IsFailure || string.IsNullOrWhiteSpace(parsed.Value.ImageStyle) || string.IsNullOrWhiteSpace(parsed.Value.Mood))
        {
            logger.LogWarning("Direção de arte por IA inválida para {Slug}; usando catálogo.", productSlug);
            return;
        }

        await fileStore.WriteTextAsync(path, JsonSerializer.Serialize(parsed.Value, JsonOptions), ct);
    }
}
