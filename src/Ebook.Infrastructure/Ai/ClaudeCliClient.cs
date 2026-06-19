using System.Diagnostics;
using System.Text;
using Ebook.Application.Ai;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Ai;

/// <summary>
/// Executa o Claude Code CLI em modo headless (claude -p) usando a assinatura Pro.
/// Concorrência 1 global (semáforo): nunca duas chamadas simultâneas à assinatura.
/// Prompt vai por stdin para evitar limites/escaping de linha de comando.
/// </summary>
public sealed class ClaudeCliClient(IOptions<AiOptions> options, ILogger<ClaudeCliClient> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string[] WindowExhaustedMarkers =
        ["usage limit", "rate limit", "limit reached", "out of extended usage"];

    /// <param name="allowedTools">
    /// Lista de ferramentas liberadas no modo headless (ex.: "Read" para análise de imagem por visão).
    /// Vazio = geração de texto pura, sem ferramentas (comportamento padrão).
    /// </param>
    public async Task<Result<string>> CompleteAsync(string prompt, CancellationToken ct = default, string? allowedTools = null)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return await RunProcessAsync(prompt, allowedTools, ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<Result<string>> RunProcessAsync(string prompt, string? allowedTools, CancellationToken ct)
    {
        var command = options.Value.ClaudeCommand;
        var toolArgs = string.IsNullOrWhiteSpace(allowedTools) ? string.Empty : $" --allowedTools \"{allowedTools}\"";
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // .cmd (shim do npm no Windows) não é executável direto pelo Process
        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c {command} -p --output-format text{toolArgs}";
        }
        else
        {
            startInfo.FileName = command;
            startInfo.Arguments = $"-p --output-format text{toolArgs}";
        }

        using var process = new Process();
        process.StartInfo = startInfo;

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(AiErrors.CliFailed($"não foi possível iniciar '{command}': {ex.Message}"));
        }

        await process.StandardInput.WriteAsync(prompt.AsMemory(), ct);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.Value.ClaudeTimeoutSeconds));

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
                // processo já terminou
            }

            return ct.IsCancellationRequested
                ? Result.Failure<string>(AiErrors.CliFailed("operação cancelada"))
                : Result.Failure<string>(AiErrors.CliFailed($"timeout após {options.Value.ClaudeTimeoutSeconds}s"));
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            logger.LogWarning("Claude CLI saiu com código {ExitCode}: {Detail}", process.ExitCode, Truncate(detail));

            return WindowExhaustedMarkers.Any(m => detail.Contains(m, StringComparison.OrdinalIgnoreCase))
                ? Result.Failure<string>(AiErrors.WindowExhausted)
                : Result.Failure<string>(AiErrors.CliFailed(Truncate(detail)));
        }

        return string.IsNullOrWhiteSpace(stdout)
            ? Result.Failure<string>(AiErrors.InvalidOutput("resposta vazia"))
            : Result.Success(stdout.Trim());
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500];
}
