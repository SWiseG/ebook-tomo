using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ebook.Application.Publishing;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Publishing;

/// <summary>
/// Cliente da API pública oficial da Kiwify (REST/OAuth). Somente leitura: lista/consulta produtos
/// e resolve a URL de checkout a partir dos links ativos. O token OAuth (Bearer) é cacheado em
/// memória até perto do vencimento. NÃO cria produtos — a criação segue manual no dashboard.
/// </summary>
public sealed class KiwifyApiClient(
    IHttpClientFactory httpFactory,
    IOptions<KiwifyOptions> options,
    ILogger<KiwifyApiClient> logger) : IKiwifyCatalog
{
    public const string HttpClientName = "kiwify-api";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // expires_in/price ora vêm como número, ora como string (a doc e a API divergem) → tolera ambos.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenExpiresAt;

    public async Task<Result<IReadOnlyList<KiwifyCatalogProduct>>> ListProductsAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (!o.HasApiCredentials)
        {
            return Result.Failure<IReadOnlyList<KiwifyCatalogProduct>>(PublishingErrors.KiwifyApiNotConfigured);
        }

        try
        {
            var http = await AuthorizedClientAsync(ct);
            if (http is null)
            {
                return Result.Failure<IReadOnlyList<KiwifyCatalogProduct>>(PublishingErrors.KiwifyApiUnavailable);
            }

            var response = await http.GetAsync($"{ApiBase(o)}/v1/products?page_size=100", ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Kiwify list products HTTP {Status}", (int)response.StatusCode);
                return Result.Failure<IReadOnlyList<KiwifyCatalogProduct>>(PublishingErrors.KiwifyApiUnavailable);
            }

            var payload = await response.Content.ReadFromJsonAsync<ListResponse>(Json, ct);
            var items = (payload?.Data ?? [])
                .Select(p => new KiwifyCatalogProduct(p.Id, p.Name ?? string.Empty, p.Status ?? string.Empty, null))
                .ToList();
            return Result.Success<IReadOnlyList<KiwifyCatalogProduct>>(items);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Falha ao listar produtos na API da Kiwify");
            return Result.Failure<IReadOnlyList<KiwifyCatalogProduct>>(PublishingErrors.KiwifyApiUnavailable);
        }
    }

    public async Task<Result<KiwifyCatalogProduct>> GetProductAsync(string kiwifyProductId, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.HasApiCredentials)
        {
            return Result.Failure<KiwifyCatalogProduct>(PublishingErrors.KiwifyApiNotConfigured);
        }

        try
        {
            var http = await AuthorizedClientAsync(ct);
            if (http is null)
            {
                return Result.Failure<KiwifyCatalogProduct>(PublishingErrors.KiwifyApiUnavailable);
            }

            var response = await http.GetAsync($"{ApiBase(o)}/v1/products/{Uri.EscapeDataString(kiwifyProductId)}", ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Kiwify get product HTTP {Status}", (int)response.StatusCode);
                return Result.Failure<KiwifyCatalogProduct>(PublishingErrors.KiwifyApiUnavailable);
            }

            var p = await response.Content.ReadFromJsonAsync<ProductDetailDto>(Json, ct);
            if (p is null)
            {
                return Result.Failure<KiwifyCatalogProduct>(PublishingErrors.KiwifyApiUnavailable);
            }

            return Result.Success(new KiwifyCatalogProduct(
                p.Id, p.Name ?? string.Empty, p.Status ?? string.Empty, ResolveCheckoutUrl(o, p.Links)));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Falha ao consultar produto na API da Kiwify");
            return Result.Failure<KiwifyCatalogProduct>(PublishingErrors.KiwifyApiUnavailable);
        }
    }

    /// <summary>URL de checkout a partir do primeiro link ativo (prioriza checkout sobre página de vendas).</summary>
    private static string? ResolveCheckoutUrl(KiwifyOptions o, IReadOnlyList<LinkDto>? links)
    {
        var link = (links ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.Id) && string.Equals(l.Status, "active", StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.IsSalesPage ? 1 : 0)
            .FirstOrDefault();
        return link is null ? null : $"{o.CheckoutBaseUrl.TrimEnd('/')}/{link.Id}";
    }

    private static string ApiBase(KiwifyOptions o) => o.PublicApiBaseUrl.TrimEnd('/');

    /// <summary>Cliente HTTP com Authorization (token válido) + header de conta, ou null se a auth falhar.</summary>
    private async Task<HttpClient?> AuthorizedClientAsync(CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        if (token is null)
        {
            return null;
        }

        var http = httpFactory.CreateClient(HttpClientName);
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        http.DefaultRequestHeaders.Add("x-kiwify-account-id", options.Value.AccountId);
        return http;
    }

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _token;
        }

        await _tokenGate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _token; // outro thread renovou enquanto esperávamos
            }

            var o = options.Value;
            var http = httpFactory.CreateClient(HttpClientName);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = o.ClientId,
                ["client_secret"] = o.ClientSecret,
            });

            var response = await http.PostAsync($"{ApiBase(o)}/v1/oauth/token", form, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Kiwify OAuth HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, ct);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return null;
            }

            // expires_in em segundos; renova 60s antes para evitar corrida na borda.
            var seconds = token.ExpiresIn > 0 ? token.ExpiresIn : 3600;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, seconds - 60));
            _token = token.AccessToken;
            return _token;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Falha ao obter token OAuth da Kiwify");
            return null;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

    private sealed record ListResponse(IReadOnlyList<ProductDto>? Data);

    private sealed record ProductDto(string Id, string? Name, string? Status);

    private sealed record ProductDetailDto(string Id, string? Name, string? Status, IReadOnlyList<LinkDto>? Links);

    private sealed record LinkDto(string Id, string? CustomName, string? Status, bool IsSalesPage);
}
