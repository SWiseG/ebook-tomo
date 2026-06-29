using Ebook.Application.Common.Text;
using Ebook.Application.Content;

namespace Ebook.Application.Tests.Content;

public class ConversionAuditTests
{
    [Fact]
    public void Parse_do_json_de_auditoria_mapeia_veredito_score_e_itens()
    {
        const string json = """
            {
              "verdict": "warn",
              "score": 72,
              "summary": "Boa estrutura; falta prova social.",
              "items": [
                { "item": "Promessa clara (4 U's)", "pass": true, "note": "ok" },
                { "item": "Prova social/autoridade", "pass": false, "note": "falta caso real" }
              ]
            }
            """;

        var result = AiJson.Parse<ConversionAuditDto>(json, "ebook.audit");

        Assert.True(result.IsSuccess);
        Assert.Equal("warn", result.Value.Verdict);
        Assert.Equal(72, result.Value.Score);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.False(result.Value.Items[1].Pass);
        Assert.Equal("Prova social/autoridade", result.Value.Items[1].Item);
    }
}
