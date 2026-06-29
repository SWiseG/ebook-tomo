namespace Ebook.Infrastructure.Media;

/// <summary>
/// Configuração dos provedores de mídia (E14). Chaves via env/appsettings (nunca no repo).
/// Pollinations vem ligado por padrão (sem chave). Gemini/Cloudflare/HuggingFace ligam quando
/// a respectiva chave é configurada. DailyLimit = 0 significa sem limite.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public ProviderOptions Gemini { get; set; } = new();
    public ProviderOptions Higgsfield { get; set; } = new();
    public ProviderOptions Cloudflare { get; set; } = new();
    public ProviderOptions HuggingFace { get; set; } = new();
    public ProviderOptions Pollinations { get; set; } = new() { Enabled = true, Model = "flux" };

    public sealed class ProviderOptions
    {
        public bool Enabled { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;    // Higgsfield (KEY_ID:KEY_SECRET)
        public string AccountId { get; set; } = string.Empty; // Cloudflare
        public string Model { get; set; } = string.Empty;
        public int DailyLimit { get; set; }
    }
}
