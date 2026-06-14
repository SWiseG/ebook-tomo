namespace Ebook.Infrastructure.Publishing;

/// <summary>
/// Configuração da Kiwify (via env/appsettings). Credenciais e token de webhook ficam fora do
/// repositório. A sessão do navegador é persistida em <see cref="StorageStatePath"/> (job kiwify-login).
/// </summary>
public sealed class KiwifyOptions
{
    public const string SectionName = "Kiwify";

    public string BaseUrl { get; set; } = "https://dashboard.kiwify.com.br";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string WebhookToken { get; set; } = string.Empty;
    public string StorageStatePath { get; set; } = "/data/kiwify/storage-state.json";
    public bool Headless { get; set; } = true;

    /// <summary>Há credenciais suficientes para tentar a automação?</summary>
    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}
