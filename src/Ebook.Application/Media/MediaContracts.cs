using Ebook.Domain.Common;

namespace Ebook.Application.Media;

/// <summary>Provedor que atendeu a geração (telemetria/cota). Ver docs/10-geracao-ia-midia.md.</summary>
public enum MediaProvider
{
    Cache,
    Gemini,
    Cloudflare,
    HuggingFace,
    Pollinations,
    Pexels,
    Unsplash,
    Pixabay,
    LocalSkia
}

/// <summary>
/// Tipo de imagem desejado (Fase 4 — Diretor de Arte por IA). Roteia a cadeia: <see cref="Photo"/>
/// prioriza bancos de foto (Pexels/Unsplash/Pixabay); <see cref="Illustration"/> prioriza geração
/// (Gemini/Cloudflare/HuggingFace/Pollinations). <see cref="Auto"/> = ordem de registro padrão.
/// O piso local (Skia) é sempre o último recurso, independentemente do tipo.
/// </summary>
public enum MediaKind
{
    Auto,
    Photo,
    Illustration
}

/// <summary>
/// Pedido de imagem. <see cref="Prompt"/> = descrição rica (provedores generativos);
/// <see cref="Query"/> = palavras-chave curtas (busca de banco de fotos). Dimensões em px.
/// </summary>
public sealed record MediaBrief(
    string Purpose,
    string Prompt,
    string Query,
    string NicheSlug,
    int Width,
    int Height,
    Guid? ProductId = null, // proveniência (Fase 3B): atribui a imagem ao produto, quando conhecido
    MediaKind Kind = MediaKind.Auto); // roteamento por tipo (Fase 4): foto vs ilustração

public sealed record MediaResult(byte[] Bytes, MediaProvider Provider, bool CacheHit);

/// <summary>
/// Único ponto de geração de imagens (E14). Cadeia free-first com cota e cache, espelhando o
/// <c>IAiGateway</c> de texto: cache → generativos grátis → banco de fotos → (falha → gradiente local).
/// </summary>
public interface IMediaGateway
{
    Task<Result<MediaResult>> GenerateAsync(MediaBrief brief, CancellationToken ct = default);
}

/// <summary>
/// Elo da cadeia (um provedor). Retorna os bytes da imagem ou null quando não consegue atender
/// (desligado, sem chave, cota, erro transitório) — o gateway tenta o próximo. Nunca lança.
/// </summary>
public interface IMediaResolver
{
    MediaProvider Provider { get; }

    /// <summary>Provedor habilitado (ex.: tem chave/flag). Desligado → o gateway pula.</summary>
    bool Enabled { get; }

    /// <summary>Limite diário de gerações (0 = sem limite). O gateway checa antes de chamar.</summary>
    int DailyLimit { get; }

    Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct);
}

public static class MediaErrors
{
    public static readonly Error NoProvider =
        new("Media.NoProvider", "Nenhum provedor de mídia conseguiu gerar a imagem.");
}
