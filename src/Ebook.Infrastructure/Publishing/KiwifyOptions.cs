namespace Ebook.Infrastructure.Publishing;

/// <summary>
/// Configuração da Kiwify (via env/appsettings). Credenciais e token de webhook ficam fora do
/// repositório. A sessão do navegador é persistida em <see cref="StorageStatePath"/> (job kiwify-login).
/// </summary>
public sealed class KiwifyOptions
{
    public const string SectionName = "Kiwify";

    public string BaseUrl { get; set; } = "https://dashboard.kiwify.com.br";

    /// <summary>E-mail da conta (apenas identifica/habilita; a autenticação real é o storageState).</summary>
    public string Email { get; set; } = string.Empty;
    public string WebhookToken { get; set; } = string.Empty;
    public string StorageStatePath { get; set; } = "/data/kiwify/storage-state.json";
    public bool Headless { get; set; } = true;

    // ── API pública oficial (REST/OAuth) — usada para resolver id + checkout de um produto já
    //    criado no dashboard. A API NÃO cria produtos; a criação segue manual. Segredos via env. ──

    /// <summary>client_id da API Key (Apps > API no dashboard).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>client_secret da API Key. Nunca versionar — env var Kiwify__ClientSecret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>account_id (header x-kiwify-account-id em todas as chamadas autenticadas).</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Base da API pública.</summary>
    public string PublicApiBaseUrl { get; set; } = "https://public-api.kiwify.com";

    /// <summary>Base das URLs de checkout (montadas a partir do id do link do produto).</summary>
    public string CheckoutBaseUrl { get; set; } = "https://pay.kiwify.com.br";

    /// <summary>
    /// Kiwify habilitado? A senha NÃO fica no app — o login é feito uma vez via `kiwify-login`
    /// (sessão salva em <see cref="StorageStatePath"/>); aqui só sinalizamos a conta configurada.
    /// </summary>
    public bool HasCredentials => !string.IsNullOrWhiteSpace(Email);

    /// <summary>Credenciais da API pública configuradas?</summary>
    public bool HasApiCredentials =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(AccountId);
}
