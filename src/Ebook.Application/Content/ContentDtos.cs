namespace Ebook.Application.Content;

/// <summary>Estrutura do outline.json gerado pelo Ebook Generator (insumo dos capítulos).</summary>
public sealed record OutlineDto(
    string Title,
    string? Subtitle,
    string Promise,
    string Tone,
    IReadOnlyList<OutlineChapterDto> Chapters);

public sealed record OutlineChapterDto(
    int N,
    string Title,
    string Goal,
    IReadOnlyList<string> KeyPoints,
    int TargetWords);

/// <summary>Recorte mínimo do sales-copy.json para extrair preço e headline.</summary>
public sealed record SalesCopyDto(string? Headline, SalesCopyPriceDto? Price);

public sealed record SalesCopyPriceDto(decimal Anchor, decimal Current);

/// <summary>Saída da passada de revisão (tiers Commercial/Premium): moldura editorial do manuscrito.</summary>
public sealed record ReviewDto(string Introduction, string Conclusion);

/// <summary>
/// Plano visual do e-book (Fase 4 — Diretor de Arte por IA): por capítulo, o tipo de imagem ideal
/// (foto vs ilustração) e a query/prompt concretos. Insumo do <c>PdfJobHandler</c> ao gerar as imagens.
/// </summary>
public sealed record VisualPlanDto(IReadOnlyList<VisualDirectiveDto> Chapters);

public sealed record VisualDirectiveDto(string Title, string Mode, string Query, string Prompt);

/// <summary>Resultado do passe de coesão (A1): patches a aplicar sobre o manuscrito montado.</summary>
public sealed record ContinuityDto(
    IReadOnlyList<BridgeDto> Bridges,
    IReadOnlyList<RemovalDto> Removals,
    IReadOnlyList<HookFixDto> HookFixes);

/// <summary>Frase-ponte inserida ao fim de um capítulo para conectar ao próximo.</summary>
public sealed record BridgeDto(int ChapterN, string Text);

/// <summary>Trecho exato a remover por ser repetição de outro trecho do manuscrito.</summary>
public sealed record RemovalDto(string Text);

/// <summary>Substituição do parágrafo de abertura de um capítulo com hook fraco.</summary>
public sealed record HookFixDto(int ChapterN, string Text);
