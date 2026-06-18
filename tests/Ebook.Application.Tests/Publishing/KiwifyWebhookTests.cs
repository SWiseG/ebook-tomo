using Ebook.Application.Publishing;
using Ebook.Domain.Sales;

namespace Ebook.Application.Tests.Publishing;

public class KiwifyWebhookTests
{
    // Payload real da Kiwify (compra aprovada): objetos aninhados CapitalCase, valores em centavos
    // (string) dentro de Commissions, UTMs em TrackingParameters.
    private const string RealApprovedJson = """
        {
          "order_id": "abc123",
          "order_ref": "REF-1",
          "order_status": "paid",
          "webhook_event_type": "order_approved",
          "payment_method": "credit_card",
          "created_at": "2026-06-18 12:00:00",
          "approved_date": "2026-06-18 12:01:00",
          "Product": { "product_id": "kw-financas", "product_name": "Virada Financeira" },
          "Customer": { "full_name": "Maria Souza", "email": "maria@example.com", "mobile": "11999990000" },
          "Commissions": {
            "charge_amount": "8063",
            "product_base_price": "8063",
            "kiwify_fee": "806",
            "my_commission": "7257",
            "currency": "BRL"
          },
          "TrackingParameters": {
            "src": null,
            "utm_source": "instagram",
            "utm_medium": "social",
            "utm_campaign": "financas-pessoais",
            "utm_content": "value"
          }
        }
        """;

    [Fact]
    public void Map_payload_real_extrai_aninhados_e_converte_centavos()
    {
        var result = KiwifyWebhookMapper.Map(RealApprovedJson);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var cmd = result.Value!;

        Assert.Equal("abc123", cmd.KiwifyOrderId);
        Assert.Equal(SaleType.Sale, cmd.Type);
        Assert.Equal("kw-financas", cmd.KiwifyProductRef);     // de Product.product_id
        Assert.Equal(80.63m, cmd.GrossAmount);                 // 8063 centavos → reais
        Assert.Equal(72.57m, cmd.NetAmount);                   // my_commission 7257 → reais
        Assert.Equal("BRL", cmd.Currency);                     // de Commissions.currency
        Assert.Equal("instagram", cmd.UtmSource);              // de TrackingParameters
        Assert.Equal("financas-pessoais", cmd.UtmCampaign);
        Assert.Equal(RealApprovedJson, cmd.RawPayloadJson);
    }

    [Theory]
    [InlineData("paid", SaleType.Sale)]
    [InlineData("approved", SaleType.Sale)]
    [InlineData("order_approved", SaleType.Sale)]
    [InlineData("compra_aprovada", SaleType.Sale)]
    [InlineData("refunded", SaleType.Refund)]
    [InlineData("compra_reembolsada", SaleType.Refund)]
    [InlineData("order.chargeback", SaleType.Chargeback)]
    [InlineData("chargedback", SaleType.Chargeback)]
    public void Map_classifica_status_gravavel(string status, SaleType expected)
    {
        var json = $$"""{ "order_id": "X", "order_status": "{{status}}" }""";

        var result = KiwifyWebhookMapper.Map(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(expected, result.Value!.Type);
    }

    [Theory]
    [InlineData("waiting_payment")]
    [InlineData("pix_created")]
    [InlineData("boleto_created")]
    [InlineData("refused")]
    [InlineData("compra_recusada")]
    [InlineData("carrinho_abandonado")]
    public void Map_ignora_eventos_nao_gravaveis(string status)
    {
        var json = $$"""{ "order_id": "X", "order_status": "{{status}}" }""";

        var result = KiwifyWebhookMapper.Map(json);

        Assert.True(result.IsSuccess); // reconhecido (200 OK)
        Assert.Null(result.Value);     // mas não grava
    }

    [Fact]
    public void Map_pix_gerado_com_valor_nao_vira_venda_fantasma()
    {
        // pix gerado já traz Commissions.charge_amount; não pode ser contado como venda
        const string json = """
            {
              "order_id": "pix-1",
              "order_status": "waiting_payment",
              "Commissions": { "charge_amount": "8063", "my_commission": "7257", "currency": "BRL" }
            }
            """;

        var result = KiwifyWebhookMapper.Map(json);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Map_aceita_payload_plano_legado_em_reais()
    {
        // resiliência: estrutura plana (valores em reais) continua mapeando
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
        Assert.NotNull(result.Value);
        var cmd = result.Value!;
        Assert.Equal(27.0m, cmd.GrossAmount);
        Assert.Equal(24.3m, cmd.NetAmount);
        Assert.Equal("kw-financas", cmd.KiwifyProductRef);
        Assert.Equal("instagram", cmd.UtmSource);
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
