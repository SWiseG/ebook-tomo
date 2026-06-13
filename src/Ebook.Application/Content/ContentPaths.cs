using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;

namespace Ebook.Application.Content;

/// <summary>Convenções de caminho do FileStore para os artefatos de conteúdo de um produto.</summary>
public static class ContentPaths
{
    public static string Outline(string slug) => $"products/{slug}/manuscript/outline.json";

    public static string Chapter(string slug, int n) => $"products/{slug}/manuscript/chapters/ch-{n:D2}.md";

    public static string Manuscript(string slug, int version) => $"products/{slug}/manuscript/manuscript.v{version}.md";

    public static string SalesCopy(string slug) => $"products/{slug}/sales-copy.json";

    /// <summary>Caminho no <c>IArtifactStore</c> (/data/artifacts), não no FileStore de conteúdo.</summary>
    public static string Pdf(string slug, int version) => $"products/{slug}/pdf/ebook.v{version}.pdf";

    /// <summary>Imagens no <c>IArtifactStore</c> (/data/artifacts).</summary>
    public static string Cover(string slug) => $"products/{slug}/images/cover.png";

    public static string Mockup(string slug) => $"products/{slug}/images/mockup.png";

    /// <summary>Override opcional de paleta por nicho, no FileStore de conteúdo (E09-03).</summary>
    public static string PaletteConfig(string nicheSlug) => $"niches/{nicheSlug}/palette.json";

    /// <summary>Lê e desserializa o outline.json; falha tipada quando ausente ou inválido.</summary>
    public static async Task<Result<OutlineDto>> ReadOutlineAsync(IFileStore fileStore, string slug, CancellationToken ct)
    {
        var content = await fileStore.ReadTextAsync(Outline(slug), ct);
        return content is null
            ? Result.Failure<OutlineDto>(ContentErrors.OutlineMissing(slug))
            : AiJson.Parse<OutlineDto>(content, "ebook.outline");
    }
}
