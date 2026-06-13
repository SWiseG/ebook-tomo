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
}
