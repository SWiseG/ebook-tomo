using Ebook.Domain.Niches;

namespace Ebook.Application.Discovery;

/// <summary>
/// Sinal de tendência normalizado por uma fonte. Métricas em 0..1 para o motor de score:
/// Volume (interesse), Competition (saturação), Monetization (intenção comercial).
/// </summary>
public sealed record TrendSignal(string Term, double Volume, double Competition, double Monetization);

/// <summary>
/// Fonte de tendências (Reddit, Google Trends/Autocomplete, Amazon...). Implementada na
/// Infrastructure; deve degradar graciosamente (lista vazia) em falha de rede.
/// </summary>
public interface ITrendSource
{
    TrendSource Source { get; }

    Task<IReadOnlyList<TrendSignal>> CollectAsync(string category, CancellationToken ct);
}
