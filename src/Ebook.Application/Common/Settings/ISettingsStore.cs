namespace Ebook.Application.Common.Settings;

/// <summary>
/// Configurações dinâmicas (tabela Setting). Valores serializados como JSON.
/// </summary>
public interface ISettingsStore
{
    Task<T> GetOrDefaultAsync<T>(string key, T defaultValue, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
}

public static class SettingKeys
{
    public const string AiMonthlyCallCap = "ai.monthlyCallCap";
    public const string AiPipelineCallBudget = "ai.pipelineCallBudget";
    public const string PublishingRequiresApproval = "publishing.requiresApproval";
    public const string MinActiveProducts = "portfolio.minActiveProducts";
    public const string DiscoveryCategories = "discovery.categories";
    public const string DiscoveryTopN = "discovery.topN";
    public const string DiscoveryScoreWeights = "discovery.scoreWeights";

    /// <summary>Base URL pública para links de checkout/pixel na LP. Vazio = caminhos relativos.</summary>
    public const string LpBaseUrl = "lp.baseUrl";

    /// <summary>
    /// Prazo REAL da oferta (ISO-8601 UTC) para o contador da LP. Vazio = sem contador
    /// (nunca usar urgência falsa). Só renderiza se o prazo for futuro no momento do render.
    /// </summary>
    public const string LpOfferDeadlineUtc = "lp.offerDeadlineUtc";

    /// <summary>Razão social no rodapé legal da LP. Vazio = usa o título do produto.</summary>
    public const string LegalCompanyName = "legal.companyName";

    /// <summary>CNPJ exibido no rodapé legal da LP. Vazio = omitido.</summary>
    public const string LegalCnpj = "legal.cnpj";

    /// <summary>E-mail de contato (vira link mailto no rodapé). Vazio = omitido.</summary>
    public const string LegalContactEmail = "legal.contactEmail";

    /// <summary>URL da política de privacidade (rodapé). Vazio = omitido.</summary>
    public const string LegalPrivacyUrl = "legal.privacyUrl";

    /// <summary>URL dos termos de uso (rodapé). Vazio = omitido.</summary>
    public const string LegalTermsUrl = "legal.termsUrl";

    /// <summary>Publicar na Kiwify automaticamente ao iniciar a publicação (default false = manual-assistido).</summary>
    public const string KiwifyAutoPublish = "kiwify.autoPublish";

    /// <summary>Publicar os posts sociais automaticamente no horário (default false = só agenda + cards).</summary>
    public const string SocialAutoPublish = "social.autoPublish";

    /// <summary>Limiares do classificador de ROI (JSON de RoiThresholds).</summary>
    public const string RoiThresholds = "roi.thresholds";

    /// <summary>Executar as decisões do otimizador automaticamente (default false = veto humano).</summary>
    public const string RoiAutoExecute = "roi.autoExecute";

    /// <summary>Gerar Reels (vídeo) semanalmente por produto ativo (default false; exige Piper+FFmpeg).</summary>
    public const string VideoEnabled = "video.enabled";

    /// <summary>Loop de aprendizado de estilo (E15): Claude vision analisa capas e grava playbook por nicho (default false; exige CLI Claude com visão).</summary>
    public const string StyleLearnEnabled = "style.learn.enabled";
}
