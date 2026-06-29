using Ebook.Application.Ai;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// IPromptLibrary sem sistema de arquivos para testes: sempre retorna falha,
/// fazendo os handlers usarem os prompts de fallback hardcoded.
/// </summary>
public sealed class NullPromptLibrary : IPromptLibrary
{
    public Task<Result<string>> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default)
        => Task.FromResult(Result.Failure<string>(new Error("prompt.null", "Sem prompt library em testes")));
}

/// <summary>
/// IPromptLibrary que sempre retorna sucesso com o nome do template como conteúdo.
/// Permite que handlers cheguem ao IMediaGateway/IAiGateway nos testes de torneio.
/// </summary>
public sealed class PassthroughPromptLibrary : IPromptLibrary
{
    public Task<Result<string>> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default)
        => Task.FromResult(Result.Success($"[prompt:{templateName}]"));
}
