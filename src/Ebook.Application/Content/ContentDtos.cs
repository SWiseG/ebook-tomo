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
