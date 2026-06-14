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

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(AccessToken)
        && (!string.IsNullOrWhiteSpace(IgUserId) || !string.IsNullOrWhiteSpace(PageId));
}
