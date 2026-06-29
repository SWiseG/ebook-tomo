using Ebook.Application.Ai;
using Ebook.Application.Common.Text;
using Ebook.Application.Content.Images;
using Microsoft.Extensions.Logging;

namespace Ebook.Infrastructure.Ai;

/// <summary>
/// QA de visão da capa full-AI (docs/14 WP-8) via Claude Code CLI (assinatura Pro): grava a capa em
/// arquivo temporário e pede ao CLI para LER a imagem (ferramenta Read) e julgar a legibilidade do
/// título. Espelha o <see cref="ClaudeVisionStyleAnalyzer"/> (semáforo/concorrência 1). Best-effort:
/// qualquer falha → score zero / veredito reprovado (o chamador usa a composição Skia determinística).
/// </summary>
public sealed class ClaudeVisionCoverQa(
    ClaudeCliClient client,
    IPromptLibrary promptLibrary,
    ILogger<ClaudeVisionCoverQa> logger) : ICoverQa
{
    public async Task<CoverScore> ScoreAsync(byte[] coverPng, string title, string nicheSlug, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"tomo-cover-qa-{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(tempPath, coverPng, ct);

            var prompt = await promptLibrary.RenderAsync("media/cover-qa", new Dictionary<string, string>
            {
                ["title"] = title,
                ["niche"] = nicheSlug,
                ["imagePath"] = tempPath.Replace('\\', '/'),
            }, ct);
            if (prompt.IsFailure)
            {
                return CoverScore.Failed;
            }

            var response = await client.CompleteAsync(prompt.Value, ct, allowedTools: "Read");
            if (response.IsFailure)
            {
                return CoverScore.Failed;
            }

            var parsed = AiJson.Parse<CoverScore>(response.Value, "cover.score");
            return parsed.IsSuccess ? parsed.Value : CoverScore.Failed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Score de visão da capa falhou; retornando score zero.");
            return CoverScore.Failed;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (IOException) { }
        }
    }

    public async Task<CoverQaVerdict> ReviewAsync(byte[] coverPng, string title, CancellationToken ct = default)
    {
        var score = await ScoreAsync(coverPng, title, string.Empty, ct);
        return new CoverQaVerdict(
            Legible: score.TitleLegible,
            TitleMatches: score.TitleLegible,
            Score: score.Score,
            Issues: score.Issues.Count > 0 ? string.Join("; ", score.Issues) : string.Empty);
    }
}
