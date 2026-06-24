using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Knowledge;

namespace Ebook.Application.Knowledge;

/// <summary>
/// Lê o playbook de estilo aprendido por nicho (E15) e devolve dicas prontas para realimentar os
/// prompts de imagem (E15-04 / docs/17 P3-12/14). Sem playbook → null (cai no comportamento padrão).
/// </summary>
public interface IStylePlaybookReader
{
    Task<string?> HintsAsync(Guid nicheId, CancellationToken ct = default);
}

public sealed class StylePlaybookReader(IKnowledgeRepository knowledge, IFileStore fileStore) : IStylePlaybookReader
{
    public async Task<string?> HintsAsync(Guid nicheId, CancellationToken ct = default)
    {
        var asset = await knowledge.GetLatestByTypeAsync(nicheId, KnowledgeAssetType.MediaStyle, ct);
        if (asset is null || await fileStore.ReadTextAsync(asset.Path, ct) is not { Length: > 0 } json)
        {
            return null;
        }

        var parsed = AiJson.Parse<MediaStyleDto>(json, "media.style");
        if (parsed.IsFailure)
        {
            return null;
        }

        var s = parsed.Value;
        var parts = new List<string>();
        if (s.PromptHints is { Count: > 0 } h) parts.AddRange(h);
        if (!string.IsNullOrWhiteSpace(s.Palette)) parts.Add($"palette: {s.Palette}");
        if (!string.IsNullOrWhiteSpace(s.Composition)) parts.Add($"composition: {s.Composition}");
        if (!string.IsNullOrWhiteSpace(s.VisualHook)) parts.Add($"hook: {s.VisualHook}");
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}
