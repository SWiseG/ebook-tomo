using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;

namespace Ebook.Application.Administration.Media;

/// <summary>Estatísticas de um provedor de mídia para o painel de telemetria (E14-08).</summary>
public sealed record MediaProviderStatDto(
    string Provider,
    int GeneratedToday,
    int GeneratedThisMonth,
    int CacheHitsToday,
    int DailyLimit,       // 0 = sem limite
    long TotalBytesToday,
    int AvgDurationMsToday);

/// <summary>Snapshot completo do Media Gateway: provedores + cache global.</summary>
public sealed record MediaTelemetryDto(
    IReadOnlyList<MediaProviderStatDto> Providers,
    int CacheHitsToday,
    int CacheEntriesTotal,
    long CacheSizeBytes);

/// <summary>Leitura de telemetria de mídia; implementado na Infrastructure (consulta direta).</summary>
public interface IMediaTelemetryReader
{
    Task<MediaTelemetryDto> GetTelemetryAsync(CancellationToken ct);
}

public sealed record GetMediaTelemetryQuery : IQuery<MediaTelemetryDto>;

public sealed class GetMediaTelemetryQueryHandler(IMediaTelemetryReader reader)
    : IQueryHandler<GetMediaTelemetryQuery, MediaTelemetryDto>
{
    public async Task<Result<MediaTelemetryDto>> HandleAsync(GetMediaTelemetryQuery query, CancellationToken ct) =>
        Result.Success(await reader.GetTelemetryAsync(ct));
}
