using System.Text.Json;
using Ebook.Application.Ai;
using Ebook.Domain.Common;

namespace Ebook.Application.Common.Text;

/// <summary>
/// Desserializa saídas JSON da IA de forma tolerante: remove cercas markdown
/// (```json ... ```) e recorta do primeiro '{' ao último '}' antes de parsear.
/// </summary>
public static class AiJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Result<T> Parse<T>(string raw, string purpose)
    {
        var json = Extract(raw);
        if (json is null)
        {
            return Result.Failure<T>(AiErrors.InvalidOutput($"{purpose}: nenhum objeto JSON na saída"));
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(json, Options);
            return value is null
                ? Result.Failure<T>(AiErrors.InvalidOutput($"{purpose}: JSON nulo"))
                : Result.Success(value);
        }
        catch (JsonException ex)
        {
            return Result.Failure<T>(AiErrors.InvalidOutput($"{purpose}: {ex.Message}"));
        }
    }

    private static string? Extract(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }
}
