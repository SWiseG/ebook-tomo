using Ebook.Application.Publishing;
using Ebook.Domain.Sales;

namespace Ebook.Application.Tests.Publishing;

public class KiwifyWebhookTests
{
    [Fact]
    public void Map_extrai_campos_e_classifica_tipo()
    {
        const string json = """
            {
              "order_id": "ABC-123",
              "order_status": "paid",
              "product_id": "kw-financas",
              "charge_amount": 27.0,
              "commissioned_value": 24.3,
              "currency": "BRL",
              "utm_source": "instagram",
              "utm_campaign": "lancamento"
            }
            """;

        var result = KiwifyWebhookMapper.Map(json);

        Assert.True(result.IsSuccess);
        var cmd = result.Value;
        Assert.Equal("ABC-123", cmd.KiwifyOrderId);
        Assert.Equal(SaleType.Sale, cmd.Type);
        Assert.Equal("kw-financas", cmd.KiwifyProductRef);
        Assert.Equal(27.0m, cmd.GrossAmount);
        Assert.Equal(24.3m, cmd.NetAmount);
        Assert.Equal("instagram", cmd.UtmSource);
        Assert.Equal(json, cmd.RawPayloadJson);
    }

    [Theory]
    [InlineData("refunded", SaleType.Refund)]
    [InlineData("order.chargeback", SaleType.Chargeback)]
    [InlineData("paid", SaleType.Sale)]
    [InlineData("approved", SaleType.Sale)]
    public void Map_classifica_status(string status, SaleType expected)
    {
        var json = $$"""{ "order_id": "X", "order_status": "{{status}}" }""";

        var result = KiwifyWebhookMapper.Map(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Type);
    }

    [Fact]
    public void Map_falha_sem_order_id()
    {
        var result = KiwifyWebhookMapper.Map("""{ "order_status": "paid" }""");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Map_falha_com_json_invalido()
    {
        var result = KiwifyWebhookMapper.Map("not json");
        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("segredo", "segredo", true)]
    [InlineData("segredo", "errado", false)]
    [InlineData("", "qualquer", false)]
    [InlineData("segredo", "", false)]
    public void IsValidToken_compara_em_tempo_fixo(string configured, string provided, bool expected)
    {
        Assert.Equal(expected, KiwifyWebhook.IsValidToken(provided, configured));
    }
}
