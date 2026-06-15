namespace Ebook.Infrastructure.Video;

/// <summary>
/// Configuração do gerador de vídeo (E10): caminhos do Piper (TTS) e do FFmpeg. Quando os
/// binários não estão configurados, os engines falham de forma tipada (costura gated).
/// </summary>
public sealed class VideoOptions
{
    public const string SectionName = "Video";

    public string PiperPath { get; set; } = string.Empty;
    public string PiperVoicePath { get; set; } = string.Empty;
    public string FfmpegPath { get; set; } = "ffmpeg";
    public int TimeoutSeconds { get; set; } = 300;

    public bool TtsConfigured =>
        !string.IsNullOrWhiteSpace(PiperPath) && !string.IsNullOrWhiteSpace(PiperVoicePath);

    public bool FfmpegConfigured => !string.IsNullOrWhiteSpace(FfmpegPath);
}
