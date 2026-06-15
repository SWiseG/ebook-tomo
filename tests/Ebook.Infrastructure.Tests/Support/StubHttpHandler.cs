using System.Net;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// Handler HTTP de teste: responde por uma fila de respostas canned e registra as requisições
/// (URL + corpo) para verificação. Sem rede.
/// </summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();

    public List<(string Url, string Body)> Requests { get; } = [];

    public StubHttpHandler Enqueue(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue((status, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
        Requests.Add((request.RequestUri!.ToString(), body));

        var (status, responseBody) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.OK, "{}");
        return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
    }
}
