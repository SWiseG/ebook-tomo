using Ebook.Application.Ai;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Ai;

/// <summary>
/// QA de visão da capa full-AI (docs/14 WP-8) via Claude Code CLI (assinatura Pro): grava a capa em
/// arquivo temporário e pede ao CLI para LER a imagem (ferramenta Read) e julgar a legibilidade do
/// título. Espelha o <see cref="ClaudeVisionStyleAnalyzer"/> (semáforo/concorrência 1). Best-effort:
/// qualquer falha → veredito reprovado (o chamador então usa a composição Skia determinística).
/// </summary>
public sealed class ClaudeVisionCoverQa(
    ClaudeCliClient client,
    IPromptLibrary promptLibrary,
    ILogger<ClaudeVisionCoverQa> logger) : ICoverQa
{
    public async Task<CoverQaVerdict> ReviewAsync(byte[] coverPng, string title, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"tomo-cover-qa-{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(tempPath, coverPng, ct);

            var prompt = await promptLibrary.RenderAsync("media/cover-qa", new Dictionary<string, string>
            {
                ["title"] = title,
                ["imagePath"] = tempPath.Replace('\\', '/'),
            }, ct);
            if (prompt.IsFailure)
            {
                return Rejected;
            }

            var response = await client.CompleteAsync(prompt.Value, ct, allowedTools: "Read");
            if (response.IsFailure)
            {
                return Rejected;
            }

            var parsed = AiJson.Parse<CoverQaVerdict>(response.Value, "cover.qa");
            return parsed.IsSuccess ? parsed.Value : Rejected;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "QA de visão da capa falhou; reprovando (fallback Skia).");
            return Rejected;
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

    private static CoverQaVerdict Rejected => new(Legible: false, TitleMatches: false, Score: 0, Issues: "QA indisponível");
}
