using System.Diagnostics;
using System.Globalization;
using System.Text;
using Ebook.Application.Video;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Video;

/// <summary>
/// TTS local via Piper (E10-02). Gated: sem binário/voz configurados, falha tipada.
/// Configurado: escreve o texto no stdin do Piper e lê o WAV gerado.
/// </summary>
public sealed class PiperTtsEngine(IOptions<VideoOptions> options, ILogger<PiperTtsEngine> logger) : ITtsEngine
{
    public async Task<Result<byte[]>> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        var o = options.Value;
        if (!o.TtsConfigured)
        {
            logger.LogWarning("Piper não configurado; síntese de voz indisponível");
            return Result.Failure<byte[]>(VideoErrors.NotConfigured("Piper (TTS)"));
        }

        var outPath = Path.Combine(Path.GetTempPath(), $"tomo-tts-{Guid.NewGuid():N}.wav");
        try
        {
            var args = $"--model \"{o.PiperVoicePath}\" --output_file \"{outPath}\"";
            var run = await VideoProcess.RunAsync(o.PiperPath, args, text, o.TimeoutSeconds, "Piper", ct);
            if (run.IsFailure)
            {
                return Result.Failure<byte[]>(run.Error);
            }

            if (!File.Exists(outPath))
            {
                return Result.Failure<byte[]>(VideoErrors.ProcessFailed("Piper", "WAV não gerado"));
            }

            return Result.Success(await File.ReadAllBytesAsync(outPath, ct));
        }
        finally
        {
            TryDelete(outPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // melhor esforço
        }
    }
}

/// <summary>
/// Montagem de Reel via FFmpeg (E10-03): slideshow 9:16 (1080×1920) das cenas + narração.
/// Gated: sem FFmpeg configurado, falha tipada. As legendas já vêm nos cards (E09).
/// </summary>
public sealed class FfmpegVideoComposer(IOptions<VideoOptions> options, ILogger<FfmpegVideoComposer> logger)
    : IVideoComposer
{
    public async Task<Result<byte[]>> RenderAsync(VideoSpec spec, CancellationToken ct = default)
    {
        var o = options.Value;
        if (!o.FfmpegConfigured)
        {
            logger.LogWarning("FFmpeg não configurado; montagem de vídeo indisponível");
            return Result.Failure<byte[]>(VideoErrors.NotConfigured("FFmpeg"));
        }

        var workDir = Path.Combine(Path.GetTempPath(), $"tomo-video-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        try
        {
            var listPath = Path.Combine(workDir, "scenes.txt");
            var list = new StringBuilder();
            for (var i = 0; i < spec.Scenes.Count; i++)
            {
                var imgPath = Path.Combine(workDir, $"scene-{i:D2}.png");
                await File.WriteAllBytesAsync(imgPath, spec.Scenes[i].Image, ct);
                var seconds = spec.Scenes[i].Seconds.ToString("0.###", CultureInfo.InvariantCulture);
                list.Append(CultureInfo.InvariantCulture, $"file '{imgPath.Replace('\\', '/')}'\n");
                list.Append(CultureInfo.InvariantCulture, $"duration {seconds}\n");
                // o demuxer concat exige repetir o último arquivo
                if (i == spec.Scenes.Count - 1)
                {
                    list.Append(CultureInfo.InvariantCulture, $"file '{imgPath.Replace('\\', '/')}'\n");
                }
            }

            await File.WriteAllTextAsync(listPath, list.ToString(), ct);

            var audioPath = Path.Combine(workDir, "narration.wav");
            await File.WriteAllBytesAsync(audioPath, spec.Narration, ct);

            var outPath = Path.Combine(workDir, "reel.mp4");
            // slideshow 9:16: escala cobrindo + crop exato; H.264/AAC; faststart p/ streaming
            var args =
                $"-y -f concat -safe 0 -i \"{listPath}\" -i \"{audioPath}\" " +
                "-vf \"scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,setsar=1\" " +
                "-r 30 -c:v libx264 -pix_fmt yuv420p -c:a aac -b:a 128k -shortest -movflags +faststart " +
                $"\"{outPath}\"";

            var run = await VideoProcess.RunAsync(o.FfmpegPath, args, stdin: null, o.TimeoutSeconds, "FFmpeg", ct);
            if (run.IsFailure)
            {
                return Result.Failure<byte[]>(run.Error);
            }

            if (!File.Exists(outPath))
            {
                return Result.Failure<byte[]>(VideoErrors.ProcessFailed("FFmpeg", "MP4 não gerado"));
            }

            return Result.Success(await File.ReadAllBytesAsync(outPath, ct));
        }
        finally
        {
            TryDeleteDir(workDir);
        }
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // melhor esforço
        }
    }
}

/// <summary>Execução de um processo externo (Piper/FFmpeg) com timeout, stdin opcional e captura de stderr.</summary>
internal static class VideoProcess
{
    public static async Task<Result> RunAsync(
        string fileName, string arguments, string? stdin, int timeoutSeconds, string component, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardInput = stdin is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return Result.Failure(VideoErrors.ProcessFailed(component, $"não foi possível iniciar '{fileName}': {ex.Message}"));
        }

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin.AsMemory(), ct);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // já terminou
            }

            return Result.Failure(VideoErrors.ProcessFailed(component, $"timeout após {timeoutSeconds}s"));
        }

        await stdoutTask;
        var stderr = await stderrTask;
        return process.ExitCode == 0
            ? Result.Success()
            : Result.Failure(VideoErrors.ProcessFailed(component, Truncate(stderr)));
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}
