using Ebook.Application.Knowledge;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// Style analyzer fake (sem CLI/visão): devolve um playbook JSON determinístico e conta as chamadas.
/// Permite forçar falha para exercitar a degradação suave do StyleLearnJobHandler.
/// </summary>
public sealed class FakeStyleAnalyzer(bool fail = false) : IStyleAnalyzer
{
    public int Calls { get; private set; }

    public Task<Result<string>> AnalyzeAsync(byte[] imageBytes, string nicheName, CancellationToken ct = default)
    {
        Calls++;
        if (fail)
        {
            return Task.FromResult(Result.Failure<string>(new Error("Test.Vision", "visão indisponível")));
        }

        const string json = """
        {
          "summary": "Identidade editorial premium e confiável.",
          "palette": "Tons quentes de âmbar com contraste escuro.",
          "typography": "Serifada de alto contraste para autoridade.",
          "composition": "Título dominante, amplo espaço negativo.",
          "visualHook": "Acento dourado guiando o olhar à promessa.",
          "promptHints": ["warm gold accents", "clean editorial layout", "premium serif headline"]
        }
        """;
        return Task.FromResult(Result.Success(json));
    }
}
