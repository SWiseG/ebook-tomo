namespace Ebook.Application.Discovery;

public static class DiscoveryJobs
{
    public const string Discover = "trends.discover";

    /// <summary>Uma descoberta por ciclo (mês) — chave de idempotência natural.</summary>
    public static string DiscoverKey(int cycle) => $"discover:{cycle}";
}

public sealed record DiscoverNichesJobPayload(int? TopN = null);
