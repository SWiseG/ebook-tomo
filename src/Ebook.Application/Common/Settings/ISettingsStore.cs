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

    /// <summary>Publicar na Kiwify automaticamente ao iniciar a publicação (default false = manual-assistido).</summary>
    public const string KiwifyAutoPublish = "kiwify.autoPublish";

    /// <summary>Publicar os posts sociais automaticamente no horário (default false = só agenda + cards).</summary>
    public const string SocialAutoPublish = "social.autoPublish";

    /// <summary>Limiares do classificador de ROI (JSON de RoiThresholds).</summary>
    public const string RoiThresholds = "roi.thresholds";

    /// <summary>Executar as decisões do otimizador automaticamente (default false = veto humano).</summary>
    public const string RoiAutoExecute = "roi.autoExecute";
}
