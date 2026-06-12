using Ebook.Domain.Common;
using Ebook.Domain.Products;

namespace Ebook.Application.Ai;

public enum AiProviderKind
{
    Cache,
    Knowledge,
    Template,
    ClaudeCli,
    ClaudeApi
}

public sealed record AiRequest(
    string Purpose,
    string PromptTemplate,
    IReadOnlyDictionary<string, string> Variables,
    QualityTier Tier = QualityTier.Commercial,
    int MaxOutputTokensEst = 2000,
    Guid? ProductId = null);

public sealed record AiResponse(
    string Content,
    AiProviderKind Provider,
    bool CacheHit,
    int DurationMs);

/// <summary>
/// Único ponto de acesso a IA da plataforma. Cadeia de resolução:
/// cache → conhecimento reutilizável → template → Claude CLI (assinatura Pro)
/// → Claude API (fallback opcional, desligado por padrão).
/// </summary>
public interface IAiGateway
{
    Task<Result<AiResponse>> CompleteAsync(AiRequest request, CancellationToken ct = default);
}

/// <summary>
/// Biblioteca de prompts versionada em /prompts. Templates com placeholders {{var}}.
/// </summary>
public interface IPromptLibrary
{
    Task<Result<string>> RenderAsync(string templateName, IReadOnlyDictionary<string, string> variables, CancellationToken ct = default);
}

public static class AiErrors
{
    public static readonly Error BudgetExceeded =
        new("Ai.BudgetExceeded", "Orçamento de chamadas de IA do período foi atingido; requisição enfileirada para a próxima janela.");

    public static readonly Error WindowExhausted =
        new("Ai.WindowExhausted", "Janela de uso da assinatura Claude Pro esgotada; tente novamente mais tarde.");

    public static Error CliFailed(string detail) => new("Ai.CliFailed", $"Claude CLI falhou: {detail}");

    public static Error InvalidOutput(string detail) => new("Ai.InvalidOutput", $"Saída da IA inválida: {detail}");

    public static Error TemplateNotFound(string name) => new("Ai.TemplateNotFound", $"Template de prompt não encontrado: {name}");

    public static Error MissingVariable(string name) => new("Ai.MissingVariable", $"Variável obrigatória ausente no prompt: {name}");
}
