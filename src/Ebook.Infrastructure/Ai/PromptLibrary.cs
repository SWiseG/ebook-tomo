using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Ebook.Application.Ai;
using Ebook.Domain.Common;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Diretório da biblioteca de prompts versionada.</summary>
    public string PromptsPath { get; set; } = "./prompts";

    /// <summary>Comando do Claude Code CLI (assinatura Pro). No Windows dev: "claude.cmd".</summary>
    public string ClaudeCommand { get; set; } = "claude";

    public int ClaudeTimeoutSeconds { get; set; } = 300;

    /// <summary>Teto padrão de chamadas CLI/mês quando não configurado em Settings.</summary>
    public int DefaultMonthlyCallCap { get; set; } = 1500;
}

/// <summary>
/// Carrega templates de /prompts/{nome}.md e substitui placeholders {{var}}.
/// Cache em memória invalidado por timestamp do arquivo (hot-reload em dev).
/// </summary>
public sealed partial class PromptLibrary(IOptions<AiOptions> options) : IPromptLibrary
{
    private readonly string _root = Path.GetFullPath(options.Value.PromptsPath);
    private readonly ConcurrentDictionary<string, (DateTime WriteTimeUtc, string Content)> _cache = new();

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderRegex();

    public async Task<Result<string>> RenderAsync(string templateName, IReadOnlyDictionary<string, string> variables, CancellationToken ct = default)
    {
        var path = Path.GetFullPath(Path.Combine(_root, templateName + ".md"));
        if (!path.StartsWith(_root, StringComparison.Ordinal) || !File.Exists(path))
        {
            return Result.Failure<string>(AiErrors.TemplateNotFound(templateName));
        }

        var writeTime = File.GetLastWriteTimeUtc(path);
        if (!_cache.TryGetValue(path, out var cached) || cached.WriteTimeUtc != writeTime)
        {
            cached = (writeTime, await File.ReadAllTextAsync(path, ct));
            _cache[path] = cached;
        }

        string? missing = null;
        var rendered = PlaceholderRegex().Replace(cached.Content, match =>
        {
            var key = match.Groups[1].Value;
            if (variables.TryGetValue(key, out var value))
            {
                return value;
            }

            missing ??= key;
            return match.Value;
        });

        return missing is not null
            ? Result.Failure<string>(AiErrors.MissingVariable(missing))
            : Result.Success(rendered);
    }
}
