namespace Ebook.Infrastructure.Social;

/// <summary>
/// Configuração do Meta (Graph API: Página Facebook + Instagram Business) via env/appsettings.
/// Token de longa duração e ids ficam fora do repositório.
/// </summary>
public sealed class MetaOptions
{
    public const string SectionName = "Meta";

    public string GraphApiVersion { get; set; } = "v21.0";
    public string ApiBase { get; set; } = "https://graph.facebook.com";
    public string PageId { get; set; } = string.Empty;
    public string IgUserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Base HTTPS pública onde /media/* é acessível (a Graph API busca image_url/video_url aqui).</summary>
    public string PublicMediaBaseUrl { get; set; } = string.Empty;

    /// <summary>Tentativas de polling do container de Reel até ficar FINISHED.</summary>
    public int ReelPollAttempts { get; set; } = 12;
    public int ReelPollDelaySeconds { get; set; } = 5;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(AccessToken)
        && (!string.IsNullOrWhiteSpace(IgUserId) || !string.IsNullOrWhiteSpace(PageId));

    public bool MediaConfigured => !string.IsNullOrWhiteSpace(PublicMediaBaseUrl);
}
