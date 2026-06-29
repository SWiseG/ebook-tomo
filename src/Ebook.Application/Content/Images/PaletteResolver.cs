using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;

namespace Ebook.Application.Content.Images;

/// <summary>
/// Resolve a identidade visual (paleta) usada por PDF, landing page e capa — a MESMA para os três,
/// garantindo coerência (docs/14 WP-1). Cadeia de prioridade: paleta do produto (gerada por IA,
/// docs/14 WP-2) → override por nicho → catálogo determinístico por categoria.
///
/// Centraliza o que antes estava duplicado/divergente em cada job (o Pdf nem lia o override).
/// </summary>
public interface IPaletteResolver
{
    /// <param name="productSlug">Slug do produto; <c>null</c> em ferramentas sem produto (ex.: LP Lab).</param>
    Task<NichePalette> ResolveAsync(string? productSlug, string nicheSlug, CancellationToken ct = default);
}

public sealed class PaletteResolver(IFileStore fileStore) : IPaletteResolver
{
    public async Task<NichePalette> ResolveAsync(string? productSlug, string nicheSlug, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(productSlug)
            && await TryReadAsync(ContentPaths.ProductPalette(productSlug), ct) is { } product)
        {
            return product;
        }

        if (await TryReadAsync(ContentPaths.PaletteConfig(nicheSlug), ct) is { } niche)
        {
            return niche;
        }

        return PaletteCatalog.ForNiche(nicheSlug);
    }

    // Lê e valida uma paleta persistida; null quando ausente, inválida ou sem cor de fundo.
    private async Task<NichePalette?> TryReadAsync(string path, CancellationToken ct)
    {
        var json = await fileStore.ReadTextAsync(path, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var parsed = AiJson.Parse<NichePalette>(json, "palette");
        return parsed.IsSuccess && !string.IsNullOrWhiteSpace(parsed.Value.Background) ? parsed.Value : null;
    }
}
