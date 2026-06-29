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

    /// <summary>Banner da vitrine Kiwify/Hotmart (~300×250). <c>IArtifactStore</c>. docs/17 P1-7.</summary>
    public static string Banner(string slug) => $"products/{slug}/images/banner.png";

    /// <summary>Ilustração de herói da landing page, gerada por IA (E14). <c>IArtifactStore</c>.</summary>
    public static string LpHero(string slug) => $"products/{slug}/images/lp-hero.png";

    /// <summary>Bundle da landing page (HTML auto-contido) no <c>IArtifactStore</c> (/data/artifacts).</summary>
    public static string LpBundle(string slug) => $"products/{slug}/lp/index.html";

    /// <summary>Calendário de conteúdo social (FileStore de conteúdo).</summary>
    public static string SocialCalendar(string slug) => $"products/{slug}/social/calendar.json";

    /// <summary>Card social de um post no <c>IArtifactStore</c> (/data/artifacts).</summary>
    public static string SocialCard(string slug, int day) => $"products/{slug}/images/card-{day:D2}.png";

    /// <summary>Slide n de um carrossel social no <c>IArtifactStore</c> (/data/artifacts).</summary>
    public static string SocialSlide(string slug, int day, int n) => $"products/{slug}/images/card-{day:D2}-{n}.png";

    /// <summary>Reel (vídeo 9:16) no <c>IArtifactStore</c> (/data/artifacts).</summary>
    public static string VideoReel(string slug, int n) => $"products/{slug}/video/reel-{n}.mp4";

    /// <summary>Roteiro do Reel (FileStore de conteúdo).</summary>
    public static string VideoScript(string slug) => $"products/{slug}/video/script.json";

    /// <summary>Sentinela que indica que o passe de coesão (A1) já foi aplicado ao manuscrito.</summary>
    public static string ContinuityMarker(string slug) => $"products/{slug}/manuscript/continuity.done";

    /// <summary>Override opcional de paleta por nicho, no FileStore de conteúdo (E09-03).</summary>
    public static string PaletteConfig(string nicheSlug) => $"niches/{nicheSlug}/palette.json";

    /// <summary>Paleta gerada por produto (docs/14 WP-2), no FileStore de conteúdo. Tem prioridade
    /// sobre a paleta do nicho — dá variedade por produto mantendo PDF, LP e capa coerentes.</summary>
    public static string ProductPalette(string slug) => $"products/{slug}/palette.json";

    /// <summary>Direção de arte de imagens do produto (docs/15 Frente A), no FileStore de conteúdo.</summary>
    public static string ProductBrand(string slug) => $"products/{slug}/brand.json";

    /// <summary>Lê e desserializa o outline.json; falha tipada quando ausente ou inválido.</summary>
    public static async Task<Result<OutlineDto>> ReadOutlineAsync(IFileStore fileStore, string slug, CancellationToken ct)
    {
        var content = await fileStore.ReadTextAsync(Outline(slug), ct);
        return content is null
            ? Result.Failure<OutlineDto>(ContentErrors.OutlineMissing(slug))
            : AiJson.Parse<OutlineDto>(content, "ebook.outline");
    }
}
