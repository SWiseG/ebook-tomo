using Ebook.Domain.Common;

namespace Ebook.Application.Knowledge;

/// <summary>
/// E15-02 — Analisa uma imagem de mídia já gerada (capa/card) e devolve um playbook de estilo em JSON
/// (resumo, paleta, tipografia, composição, gancho visual, dicas de prompt). A implementação real usa
/// a visão do Claude Code CLI (assinatura Pro). Costura gated: sem CLI de visão disponível, retorna
/// falha tipada e o job de aprendizado apenas registra e segue (não vira dead-letter).
/// </summary>
public interface IStyleAnalyzer
{
    Task<Result<string>> AnalyzeAsync(byte[] imageBytes, string nicheName, CancellationToken ct = default);
}

/// <summary>
/// Playbook de estilo aprendido por nicho (E15-03). O conteúdo bruto (JSON) vive no FileStore; o
/// índice é um <c>KnowledgeAsset(MediaStyle)</c>. <see cref="PromptHints"/> realimenta os prompts de
/// geração de imagem (E15-04) e os presets do Skia local (E15-05) em incrementos seguintes.
/// </summary>
public sealed record MediaStyleDto(
    string Summary,
    string? Palette,
    string? Typography,
    string? Composition,
    string? VisualHook,
    IReadOnlyList<string>? PromptHints);
