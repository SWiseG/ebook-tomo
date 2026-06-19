using Ebook.Application.Ai;
using Ebook.Application.Knowledge;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Ai;

/// <summary>
/// E15-02 — Análise de estilo por visão usando o Claude Code CLI (assinatura Pro). Grava a imagem em
/// arquivo temporário e pede ao CLI para LER a imagem pelo caminho (a ferramenta Read do CLI enxerga
/// imagens) e devolver um playbook JSON. Reaproveita o semáforo do <see cref="ClaudeCliClient"/>
/// (concorrência 1). NÃO passa pelo cache do AiGateway — é análise de imagem, não texto reutilizável.
/// Costura best-effort/gated: qualquer falha vira <see cref="Result"/> de falha e o job de aprendizado
/// apenas registra e segue (sem dead-letter).
/// </summary>
public sealed class ClaudeVisionStyleAnalyzer(
    ClaudeCliClient client,
    IPromptLibrary promptLibrary,
    ILogger<ClaudeVisionStyleAnalyzer> logger) : IStyleAnalyzer
{
    public async Task<Result<string>> AnalyzeAsync(byte[] imageBytes, string nicheName, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"tomo-style-{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(tempPath, imageBytes, ct);

            var prompt = await promptLibrary.RenderAsync("media/style-analyze", new Dictionary<string, string>
            {
                ["niche"] = nicheName,
                ["imagePath"] = tempPath.Replace('\\', '/'),
                ["language"] = "pt-BR",
            }, ct);
            if (prompt.IsFailure)
            {
                return Result.Failure<string>(prompt.Error);
            }

            // libera a ferramenta Read para o CLI conseguir abrir a imagem no modo headless
            return await client.CompleteAsync(prompt.Value, ct, allowedTools: "Read");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Análise de estilo por visão falhou para o nicho {Niche}", nicheName);
            return Result.Failure<string>(AiErrors.CliFailed(ex.Message));
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
                // limpeza best-effort
            }
        }
    }
}
