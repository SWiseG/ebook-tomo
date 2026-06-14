namespace Ebook.Application.Content.Lp;

/// <summary>
/// Recorte completo do sales-copy.json para alimentar a landing page (E06-02).
/// Campos opcionais: a copy pode vir parcial conforme o tier; o builder usa fallbacks.
/// </summary>
public sealed record LpCopyDto(
    string? Headline,
    string? Subheadline,
    IReadOnlyList<string>? Bullets,
    string? PainSection,
    string? SolutionSection,
    IReadOnlyList<LpFaqDto>? Faq,
    LpPriceDto? Price,
    IReadOnlyList<string>? Bonuses);

public sealed record LpFaqDto(string? Q, string? A);

public sealed record LpPriceDto(decimal Anchor, decimal Current);
