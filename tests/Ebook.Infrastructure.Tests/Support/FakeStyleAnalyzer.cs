using Ebook.Application.Knowledge;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

public sealed class FakeStyleAnalyzer : IStyleAnalyzer
{
    public Task<Result<string>> AnalyzeAsync(byte[] imageBytes, string nicheName, CancellationToken ct = default)
    {
        var json = """
            {
              "summary": "Estilo minimalista com cores vibrantes",
              "palette": "#1A1A2E, #E94560, #F5F5F5",
              "typography": "Sans-serif bold para títulos",
              "composition": "Centralizado com muito espaço negativo",
              "visualHook": "Contraste forte entre fundo escuro e texto claro",
              "promptHints": ["dark background", "bold typography", "high contrast"]
            }
            """;
        return Task.FromResult(Result.Success(json));
    }
}
