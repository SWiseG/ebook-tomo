namespace Ebook.Application.Content.Lp;

/// <summary>
/// Recorte completo do sales-copy.json para alimentar a landing page (E06-02).
/// Campos opcionais: a copy pode vir parcial conforme o tier; o builder usa fallbacks.
///
/// LP 2.0 (docs/12): os campos do segundo bloco enriquecem a página com seções de alta
/// conversão. Honestidade inegociável — campos que dependem de dados reais (rating,
/// testimonials, mediaLogos, stats) só são preenchidos quando reais; o prompt NÃO os
/// inventa, e o builder simplesmente omite o bloco quando ausente.
/// </summary>
public sealed record LpCopyDto(
    string? Headline,
    string? Subheadline,
    IReadOnlyList<string>? Bullets,
    string? PainSection,
    string? SolutionSection,
    IReadOnlyList<LpFaqDto>? Faq,
    LpPriceDto? Price,
    IReadOnlyList<string>? Bonuses,
    // ── LP 2.0 ──────────────────────────────────────────────────────────────
    /// <summary>Pílula curta de credibilidade no topo do hero (enquadrada por benefício, não número falso).</summary>
    string? ProofPill = null,
    /// <summary>Selos factuais e verdadeiros (ex.: "Garantia de 7 dias", "Acesso imediato", "Pix, cartão ou boleto").</summary>
    IReadOnlyList<string>? TrustBadges = null,
    /// <summary>Etapas do método ("Como funciona"), derivadas do conteúdo real do e-book.</summary>
    IReadOnlyList<LpStepDto>? Steps = null,
    /// <summary>Bônus com nome, descrição e valor percebido (para o empilhamento com ancoragem).</summary>
    IReadOnlyList<LpBonusDto>? BonusItems = null,
    /// <summary>Garantia / reversão de risco (real — direito de arrependimento CDC art. 49 = 7 dias).</summary>
    LpGuaranteeDto? Guarantee = null,
    /// <summary>Seção de CTA final (fechamento emocional + botão).</summary>
    LpFinalCtaDto? FinalCta = null,
    // ── Somente quando houver dados REAIS (futuro: webhooks/reviews) — nunca inventar ──
    LpRatingDto? Rating = null,
    IReadOnlyList<LpStatDto>? Stats = null,
    IReadOnlyList<LpTestimonialDto>? Testimonials = null,
    IReadOnlyList<string>? MediaLogos = null,
    LpAuthorDto? Author = null);

public sealed record LpFaqDto(string? Q, string? A);

/// <summary>Preço em BRL. Anchor = referência riscada; Current = venda; Installments = parcelas (Kiwify).</summary>
public sealed record LpPriceDto(decimal Anchor, decimal Current, int? Installments = null);

public sealed record LpStepDto(string? Label, string? Title, string? Description);

public sealed record LpBonusDto(string? Name, string? Description, decimal? Value);

public sealed record LpGuaranteeDto(string? Title, string? Body, int? Days);

public sealed record LpFinalCtaDto(string? Headline, string? Body, string? Button);

public sealed record LpRatingDto(decimal Value, int Count);

public sealed record LpStatDto(string? Value, string? Label);

public sealed record LpTestimonialDto(string? Quote, string? Name, string? Role, string? Result);

public sealed record LpAuthorDto(
    string? Name,
    string? Title,
    string? Credentials,
    string? Bio,
    IReadOnlyList<string>? Highlights);

/// <summary>Dados legais do rodapé (E06 / Fase 5). Vêm de configuração, não da IA. Campos vazios são omitidos.</summary>
public sealed record LpLegalDto(
    string? CompanyName,
    string? Cnpj,
    string? ContactEmail,
    string? PrivacyUrl,
    string? TermsUrl);
