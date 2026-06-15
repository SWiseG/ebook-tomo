using Ebook.Domain.Common;

namespace Ebook.Application.Video;

/// <summary>Tipos/chaves dos jobs de vídeo (E10).</summary>
public static class VideoJobs
{
    public const string Generate = "video.generate";

    public static string GenerateKey(Guid productId, int week) => $"video:{productId}:{week}";
}

public sealed record VideoJobPayload(Guid ProductId);

/// <summary>Saída da IA: roteiro de um Reel (gancho + cenas + legenda).</summary>
public sealed record VideoScriptDto(
    string Hook,
    IReadOnlyList<VideoSceneDto> Scenes,
    string Caption,
    IReadOnlyList<string>? Hashtags);

public sealed record VideoSceneDto(string Narration, string OnScreen, double Seconds);

/// <summary>Uma cena renderizada do vídeo: imagem (card 9:16) + duração.</summary>
public sealed record VideoScene(byte[] Image, double Seconds);

/// <summary>Especificação para montar o MP4: cenas (imagens + durações) + narração (áudio).</summary>
public sealed record VideoSpec(IReadOnlyList<VideoScene> Scenes, byte[] Narration);

/// <summary>
/// Síntese de voz local (E10-02; Piper, pt-BR). Costura gated: sem binário/voz configurados,
/// falha de forma tipada. Implementado na Infrastructure.
/// </summary>
public interface ITtsEngine
{
    Task<Result<byte[]>> SynthesizeAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// Montagem de vídeo (E10-03; FFmpeg, slideshow 9:16 com narração). Costura gated.
/// Implementado na Infrastructure.
/// </summary>
public interface IVideoComposer
{
    Task<Result<byte[]>> RenderAsync(VideoSpec spec, CancellationToken ct = default);
}

public static class VideoErrors
{
    public static Error ProductNotFound(Guid id) =>
        new("Video.Product.NotFound", $"Produto {id} não encontrado.");

    public static Error NotConfigured(string component) =>
        new("Video.NotConfigured", $"{component} não configurado (defina os caminhos do Piper/FFmpeg).");

    public static Error ProcessFailed(string component, string detail) =>
        new("Video.ProcessFailed", $"{component} falhou: {detail}");
}
