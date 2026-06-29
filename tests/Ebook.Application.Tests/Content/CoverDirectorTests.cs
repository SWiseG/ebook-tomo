using Ebook.Application.Ai;
using Ebook.Application.Content.Images;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Application.Tests.Content;

public class CoverDirectorTests
{
    [Fact]
    public async Task Plano_valido_e_parseado_e_normalizado()
    {
        var ai = new StubAi("""
            {
              "eyebrow": "Guia Completo",
              "subtitle": "Do caos ao controle",
              "features": [
                { "text": "Elimine dívidas", "icon": "money" },
                { "text": "  ", "icon": "x" },
                { "text": "Crie sua reserva", "icon": "" }
              ],
              "seal": "MÉTODO VALIDADO",
              "scene": "person at a desk, editorial photo, no text",
              "layout": "classic"
            }
            """);

        var plan = await new CoverDirector(ai, NullLogger<CoverDirector>.Instance)
            .PlanAsync("Título", "Sub", "financas", "cap 1; cap 2");

        Assert.NotNull(plan);
        Assert.Equal("Guia Completo", plan!.Eyebrow);
        Assert.Equal(2, plan.Features.Count);              // o benefício em branco foi descartado
        Assert.Equal("check", plan.Features[1].Icon);      // ícone vazio → padrão "check"
    }

    [Fact]
    public async Task Plano_sem_beneficios_vira_null()
    {
        var ai = new StubAi("""{ "eyebrow": "X", "features": [], "seal": "Y", "scene": "z" }""");

        var plan = await new CoverDirector(ai, NullLogger<CoverDirector>.Instance)
            .PlanAsync("T", null, "saude", "t");

        Assert.Null(plan);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Verdict_aceita_so_quando_legivel_e_titulo_bate(bool legible, bool titleMatches, bool accepted)
    {
        Assert.Equal(accepted, new CoverQaVerdict(legible, titleMatches, 90, "").Accepted);
    }

    private sealed class StubAi(string content) : IAiGateway
    {
        public Task<Result<AiResponse>> CompleteAsync(AiRequest request, CancellationToken ct = default) =>
            Task.FromResult(Result.Success(new AiResponse(content, AiProviderKind.ClaudeCli, false, 1)));
    }
}
