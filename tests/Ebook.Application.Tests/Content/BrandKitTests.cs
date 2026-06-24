using Ebook.Application.Ai;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Application.Knowledge;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Application.Tests.Content;

public class BrandKitTests
{
    [Fact]
    public void Decorate_acopla_estilo_mood_e_sujeito_ao_brief()
    {
        var brand = new ProductBrand("calm", "documentary photo", "real people");
        var p = brand.Decorate("a desk scene");
        Assert.Contains("documentary photo", p, StringComparison.Ordinal);
        Assert.Contains("calm", p, StringComparison.Ordinal);
        Assert.Contains("real people", p, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Director_gera_e_persiste_e_resolver_le()
    {
        var store = new FakeStore();
        var ai = new StubAi("""{ "mood": "bold", "imageStyle": "vivid editorial", "subjectGuidance": "entrepreneurs" }""");
        await new BrandDirector(ai, store, new NoPlaybook(), NullLogger<BrandDirector>.Instance)
            .EnsureAsync("p", "marketing", Guid.NewGuid(), "T");

        var brand = await new BrandResolver(store).ResolveAsync("p", "marketing");
        Assert.Equal("vivid editorial", brand.ImageStyle);
    }

    [Fact]
    public async Task Sem_brand_resolver_cai_no_catalogo()
    {
        var brand = await new BrandResolver(new FakeStore()).ResolveAsync(null, "financas-autonomos");
        Assert.Equal(BrandCatalog.For(NicheCategory.Finance).ImageStyle, brand.ImageStyle);
    }

    private sealed class NoPlaybook : IStylePlaybookReader
    {
        public Task<string?> HintsAsync(Guid nicheId, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    private sealed class StubAi(string c) : IAiGateway
    {
        public Task<Result<AiResponse>> CompleteAsync(AiRequest r, CancellationToken ct = default) =>
            Task.FromResult(Result.Success(new AiResponse(c, AiProviderKind.ClaudeCli, false, 1)));
    }

    private sealed class FakeStore : IFileStore
    {
        private readonly Dictionary<string, string> f = [];
        public Task<StoredFile> WriteTextAsync(string p, string c, CancellationToken ct = default)
        { f[p] = c; return Task.FromResult(new StoredFile(p, "h", c.Length)); }
        public Task<string?> ReadTextAsync(string p, CancellationToken ct = default) => Task.FromResult(f.GetValueOrDefault(p));
        public bool Exists(string p) => f.ContainsKey(p);
    }
}
