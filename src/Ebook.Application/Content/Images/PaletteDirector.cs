using System.Text.Json;
using System.Text.RegularExpressions;
using Ebook.Application.Ai;
using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ebook.Application.Content.Images;

/// <summary>
/// Gera, por IA, a identidade visual (paleta) de um PRODUTO — cores + fontes (docs/14 WP-2) — e
/// persiste em <see cref="ContentPaths.ProductPalette"/>, que PDF, LP e capa leem via
/// <see cref="IPaletteResolver"/>. Dá variedade por produto mantendo os três artefatos coerentes.
///
/// Idempotente (pula se já existe). Robusto: valida cores (#RRGGBB) e fontes (só o set embarcado);
/// qualquer falha/saída inválida → não persiste e o resolver cai no catálogo determinístico.
/// </summary>
public interface IPaletteDirector
{
    Task EnsureAsync(string productSlug, string nicheSlug, string title, CancellationToken ct = default);
}

public sealed partial class PaletteDirector(
    IAiGateway aiGateway,
    IFileStore fileStore,
    ILogger<PaletteDirector> logger) : IPaletteDirector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnsureAsync(string productSlug, string nicheSlug, string title, CancellationToken ct = default)
    {
        var path = ContentPaths.ProductPalette(productSlug);
        if (fileStore.Exists(path))
        {
            return;
        }

        var category = NicheStyleCatalog.Classify(nicheSlug);
        var basePalette = NicheStyleCatalog.For(category);

        var ai = await aiGateway.CompleteAsync(new AiRequest(
            Purpose: "ebook.palette",
            PromptTemplate: "ebook/palette",
            Variables: new Dictionary<string, string>
            {
                ["niche"] = nicheSlug,
                ["title"] = title,
                ["category"] = category.ToString(),
                ["baseBackground"] = basePalette.Background,
                ["baseAccent"] = basePalette.Accent,
            },
            MaxOutputTokensEst: 300), ct);

        if (ai.IsFailure)
        {
            logger.LogInformation("Paleta por IA indisponível para {Slug} ({Error}); usando catálogo.",
                productSlug, ai.Error.Code);
            return;
        }

        var parsed = AiJson.Parse<NichePalette>(ai.Value.Content, "ebook.palette");
        if (parsed.IsFailure || !IsHex(parsed.Value.Background) || !IsHex(parsed.Value.Accent) || !IsHex(parsed.Value.OnDark))
        {
            logger.LogWarning("Paleta por IA inválida para {Slug}; usando catálogo.", productSlug);
            return;
        }

        // Sanitiza fontes para o set embarcado (fora dele, cairia no fallback feio do renderizador).
        var safe = parsed.Value with
        {
            HeadingFont = SafeFont(parsed.Value.HeadingFont, basePalette.HeadingFont),
            BodyFont = SafeFont(parsed.Value.BodyFont, basePalette.BodyFont),
            DisplayFont = SafeFont(parsed.Value.Display, basePalette.Display),
        };

        await fileStore.WriteTextAsync(path, JsonSerializer.Serialize(safe, JsonOptions), ct);
        logger.LogInformation("Paleta por IA gerada para {Slug}: fundo {Bg}, display {Font} ({Provider})",
            productSlug, safe.Background, safe.DisplayFont, ai.Value.Provider);
    }

    private static string SafeFont(string? candidate, string fallback) =>
        !string.IsNullOrWhiteSpace(candidate) && NicheStyleCatalog.AllowedFonts.Contains(candidate.Trim())
            ? candidate.Trim()
            : fallback;

    private static bool IsHex(string? value) => !string.IsNullOrWhiteSpace(value) && HexColor().IsMatch(value);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
