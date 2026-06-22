using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;

namespace Ebook.Application.Administration.Sources;

/// <summary>
/// Estatísticas de uma fonte externa (Fase 3 / docs/13 §4). Unifica geração de TEXTO (AI Gateway:
/// Claude e afins) e de IMAGEM (Media Gateway: Gemini, Pollinations, Pexels, Local Skia…).
/// </summary>
public sealed record SourceStatDto(
    string Provider,
    string Kind,                 // "Texto" | "Imagem"
    int GeneratedToday,
    int GeneratedThisMonth,
    long TokensToday,            // texto (0 para imagem)
    long BytesToday,             // imagem (0 para texto)
    int AvgDurationMsToday);

/// <summary>Snapshot unificado de todas as fontes externas + cache.</summary>
public sealed record SourcesTelemetryDto(
    IReadOnlyList<SourceStatDto> Sources,
    int CacheHitsToday,
    int MediaCacheEntriesTotal,
    long MediaCacheSizeBytes);

/// <summary>Leitura de telemetria unificada de fontes; implementado na Infrastructure (consulta direta).</summary>
public interface ISourcesTelemetryReader
{
    Task<SourcesTelemetryDto> GetTelemetryAsync(CancellationToken ct);
}

public sealed record GetSourcesTelemetryQuery : IQuery<SourcesTelemetryDto>;

public sealed class GetSourcesTelemetryQueryHandler(ISourcesTelemetryReader reader)
    : IQueryHandler<GetSourcesTelemetryQuery, SourcesTelemetryDto>
{
    public async Task<Result<SourcesTelemetryDto>> HandleAsync(GetSourcesTelemetryQuery query, CancellationToken ct) =>
        Result.Success(await reader.GetTelemetryAsync(ct));
}
