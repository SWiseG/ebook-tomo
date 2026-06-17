using Ebook.Domain.Common;

namespace Ebook.Domain.Social;

/// <summary>Plataforma de um canal. Meta = Instagram + Facebook (Graph API). Outras virão depois.</summary>
public enum ChannelPlatform
{
    Meta
}

/// <summary>
/// Canal social de um nicho (decisão: 1 conta por nicho, não por produto). Guarda as credenciais
/// de publicação (preenchidas no painel a partir do Meta) e roteia a publicação dos produtos do nicho.
/// Segredos ficam no banco (volume privado), nunca no repositório.
/// </summary>
public sealed class Channel : AggregateRoot
{
    private Channel()
    {
        Name = string.Empty;
    }

    public Guid NicheId { get; private set; }
    public string Name { get; private set; }
    public ChannelPlatform Platform { get; private set; }

    // Credenciais de publicação (Meta). Preenchidas via tela de Canais; nulas até conectar.
    public string? PageId { get; private set; }
    public string? IgUserId { get; private set; }
    public string? AccessToken { get; private set; }

    /// <summary>Base HTTPS pública onde /media/* é acessível (a Graph API busca image_url/video_url aqui).</summary>
    public string? PublicMediaBaseUrl { get; private set; }
    public DateTime? TokenExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Conectado = tem token e ao menos um destino (IG ou Página).</summary>
    public bool IsConnected =>
        !string.IsNullOrWhiteSpace(AccessToken)
        && (!string.IsNullOrWhiteSpace(IgUserId) || !string.IsNullOrWhiteSpace(PageId));

    public static Channel Create(Guid nicheId, string name, ChannelPlatform platform, DateTime utcNow) =>
        new()
        {
            NicheId = nicheId,
            Name = name,
            Platform = platform,
            CreatedAtUtc = utcNow,
        };

    public void Rename(string name) => Name = name;

    /// <summary>Conecta/atualiza as credenciais do Meta (token de longa duração + ids + base de mídia).</summary>
    public void SetCredentials(
        string? pageId, string? igUserId, string? accessToken,
        string? publicMediaBaseUrl, DateTime? tokenExpiresAtUtc)
    {
        PageId = Blank(pageId);
        IgUserId = Blank(igUserId);
        AccessToken = Blank(accessToken);
        PublicMediaBaseUrl = Blank(publicMediaBaseUrl);
        TokenExpiresAtUtc = tokenExpiresAtUtc;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
