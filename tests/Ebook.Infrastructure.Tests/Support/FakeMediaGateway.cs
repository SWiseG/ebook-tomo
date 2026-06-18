using Ebook.Application.Media;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// IMediaGateway sem rede para testes: retorna falha (sem imagem), permitindo que
/// o PdfJobHandler prossiga sem gerar ilustrações. Zero chamadas de rede/Skia.
/// </summary>
public sealed class FakeMediaGateway : IMediaGateway
{
    public int Calls { get; private set; }

    public Task<Result<MediaResult>> GenerateAsync(MediaBrief brief, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(Result.Failure<MediaResult>(new Error("media.fake", "Fake gateway — sem imagem")));
    }
}
