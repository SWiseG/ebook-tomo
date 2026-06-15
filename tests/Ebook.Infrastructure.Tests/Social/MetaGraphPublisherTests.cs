using Ebook.Application.Social;
using Ebook.Domain.Social;
using Ebook.Infrastructure.Social;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Tests.Social;

/// <summary>
/// Verifica a CONSTRUÇÃO das requisições da Graph API (não chama o Meta real): fluxo de duas
/// etapas no Instagram (container → publish) e foto no Facebook, com a image_url pública.
/// </summary>
public class MetaGraphPublisherTests
{
    private static MetaGraphPublisher Build(StubHttpHandler handler, MetaOptions options) =>
        new(new HttpClient(handler), Options.Create(options), NullLogger<MetaGraphPublisher>.Instance);

    [Fact]
    public async Task Instagram_cria_container_e_publica_com_image_url_publica()
    {
        var handler = new StubHttpHandler()
            .Enqueue("""{ "id": "container-123" }""")  // POST /media
            .Enqueue("""{ "id": "media-456" }""");      // POST /media_publish
        var publisher = Build(handler, new MetaOptions
        {
            IgUserId = "ig-1",
            AccessToken = "tok",
            PublicMediaBaseUrl = "https://cdn.tomo.com"
        });

        var result = await publisher.PublishAsync(
            new SocialPublishRequest(SocialNetwork.Instagram, "Minha legenda",
                "products/guia/images/card-01.png", "/go/guia"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("media-456", result.Value.ExternalId);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/ig-1/media", handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.Contains("image_url=https%3A%2F%2Fcdn.tomo.com%2Fmedia%2Fproducts%2Fguia%2Fimages%2Fcard-01.png",
            handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("caption=Minha", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("/ig-1/media_publish", handler.Requests[1].Url, StringComparison.Ordinal);
        Assert.Contains("creation_id=container-123", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Facebook_posta_foto_na_pagina()
    {
        var handler = new StubHttpHandler().Enqueue("""{ "id": "p", "post_id": "page_99" }""");
        var publisher = Build(handler, new MetaOptions
        {
            PageId = "page-1",
            AccessToken = "tok",
            PublicMediaBaseUrl = "https://cdn.tomo.com"
        });

        var result = await publisher.PublishAsync(
            new SocialPublishRequest(SocialNetwork.Facebook, "Olá", "products/guia/images/card-02.png", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("page_99", result.Value.ExternalId);
        Assert.Contains("/page-1/photos", handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.Contains("url=https%3A%2F%2Fcdn.tomo.com%2Fmedia%2F", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erro_da_graph_api_vira_falha_tipada()
    {
        var handler = new StubHttpHandler()
            .Enqueue("""{ "error": { "message": "Invalid OAuth token", "code": 190 } }""",
                System.Net.HttpStatusCode.BadRequest);
        var publisher = Build(handler, new MetaOptions
        {
            IgUserId = "ig-1",
            AccessToken = "bad",
            PublicMediaBaseUrl = "https://cdn.tomo.com"
        });

        var result = await publisher.PublishAsync(
            new SocialPublishRequest(SocialNetwork.Instagram, "x", "products/guia/images/card-01.png", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Social.Meta.ApiError", result.Error.Code);
    }

    [Fact]
    public async Task Sem_base_publica_de_midia_falha_como_nao_configurado()
    {
        var handler = new StubHttpHandler();
        var publisher = Build(handler, new MetaOptions { IgUserId = "ig-1", AccessToken = "tok" }); // sem PublicMediaBaseUrl

        var result = await publisher.PublishAsync(
            new SocialPublishRequest(SocialNetwork.Instagram, "x", "products/guia/images/card-01.png", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Social.NotConfigured", result.Error.Code);
        Assert.Empty(handler.Requests);
    }
}
