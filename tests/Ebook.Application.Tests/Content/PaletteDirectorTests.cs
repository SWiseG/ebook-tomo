using Ebook.Application.Ai;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ebook.Application.Tests.Content;

public class PaletteDirectorTests
{
    [Fact]
    public async Task Gera_e_persiste_paleta_da_IA_sanitizando_fonte_fora_do_set()
    {
        var store = new FakeFileStore();
        var ai = new StubAi("""
            { "background": "#101820", "accent": "#FFB703", "onDark": "#F8F9FA",
              "headingFont": "Manrope", "bodyFont": "Inter", "displayFont": "ComicSans" }
            """);
        await new PaletteDirector(ai, store, NullLogger<PaletteDirector>.Instance)
            .EnsureAsync("meu-produto", "financas", "Meu Título");

        var palette = await new PaletteResolver(store).ResolveAsync("meu-produto", "financas");
        Assert.Equal("#101820", palette.Background);
        Assert.Equal("Manrope", palette.HeadingFont);
        // "ComicSans" não está no set embarcado → cai na display do catálogo (Finance = Archivo Black)
        Assert.Equal("Archivo Black", palette.Display);
    }

    [Fact]
    public async Task Idempotente_nao_chama_a_IA_se_a_paleta_ja_existe()
    {
        var store = new FakeFileStore();
        await store.WriteTextAsync(ContentPaths.ProductPalette("p"),
            """{ "background": "#000000", "accent": "#FFFFFF", "onDark": "#EEEEEE", "headingFont": "Inter", "bodyFont": "Inter" }""");
        var ai = new StubAi("{}");

        await new PaletteDirector(ai, store, NullLogger<PaletteDirector>.Instance)
            .EnsureAsync("p", "financas", "T");

        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task Saida_invalida_nao_persiste_e_resolver_cai_no_catalogo()
    {
        var store = new FakeFileStore();
        var ai = new StubAi("{}"); // sem cores válidas

        await new PaletteDirector(ai, store, NullLogger<PaletteDirector>.Instance)
            .EnsureAsync("p", "saude", "T");

        Assert.False(store.Exists(ContentPaths.ProductPalette("p")));
        var palette = await new PaletteResolver(store).ResolveAsync("p", "saude");
        Assert.Equal(PaletteCatalog.ForNiche("saude").Background, palette.Background);
    }

    private sealed class StubAi(string content) : IAiGateway
    {
        public int Calls { get; private set; }
        public Task<Result<AiResponse>> CompleteAsync(AiRequest request, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Result.Success(new AiResponse(content, AiProviderKind.ClaudeCli, false, 1)));
        }
    }

    private sealed class FakeFileStore : IFileStore
    {
        private readonly Dictionary<string, string> files = [];
        public Task<StoredFile> WriteTextAsync(string relativePath, string content, CancellationToken ct = default)
        {
            files[relativePath] = content;
            return Task.FromResult(new StoredFile(relativePath, "hash", content.Length));
        }
        public Task<string?> ReadTextAsync(string relativePath, CancellationToken ct = default) =>
            Task.FromResult(files.GetValueOrDefault(relativePath));
        public bool Exists(string relativePath) => files.ContainsKey(relativePath);
    }
}
