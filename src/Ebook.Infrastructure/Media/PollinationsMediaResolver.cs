using Ebook.Application.Media;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// Provedor generativo gratuito SEM chave (E14-05): Pollinations. GET na URL do prompt devolve o PNG.
/// Ligado por padrão; é o último generativo antes do banco de fotos. Erros viram null (gateway tenta o próximo).
/// </summary>
public sealed class PollinationsMediaResolver(HttpClient http, IOptions<MediaOptions> options) : IMediaResolver
{
    public MediaProvider Provider => MediaProvider.Pollinations;
    public bool Enabled => options.Value.Pollinations.Enabled;
    public int DailyLimit => options.Value.Pollinations.DailyLimit;

    public async Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(options.Value.Pollinations.Model) ? "flux" : options.Value.Pollinations.Model;
        var url = $"https://image.pollinations.ai/prompt/{Uri.EscapeDataString(brief.Prompt)}"
            + $"?width={brief.Width}&height={brief.Height}&nologo=true&model={Uri.EscapeDataString(model)}";

        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return bytes.Length > 256 ? bytes : null; // descarta respostas de erro curtas
    }
}
