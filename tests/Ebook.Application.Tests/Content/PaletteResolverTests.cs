using System.Text.Json;
using Ebook.Application.Content;
using Ebook.Application.Content.Images;
using Ebook.Domain.Abstractions;

namespace Ebook.Application.Tests.Content;

public class PaletteResolverTests
{
    [Fact]
    public async Task Paleta_do_produto_tem_prioridade_sobre_nicho_e_catalogo()
    {
        var store = new FakeFileStore();
        var custom = new NichePalette("#101010", "#FFD700", "#FAFAFA", "Anton", "Inter");
        await store.WriteTextAsync(ContentPaths.ProductPalette("meu-produto"), Json(custom));
        await store.WriteTextAsync(ContentPaths.PaletteConfig("financas"), Json(
            new NichePalette("#222222", "#00FF00", "#EEEEEE", "Lora", "Lora")));

        var palette = await new PaletteResolver(store).ResolveAsync("meu-produto", "financas");

        Assert.Equal("#101010", palette.Background);
        Assert.Equal("Anton", palette.HeadingFont);
    }

    [Fact]
    public async Task Sem_paleta_de_produto_cai_no_override_do_nicho()
    {
        var store = new FakeFileStore();
        await store.WriteTextAsync(ContentPaths.PaletteConfig("financas"), Json(
            new NichePalette("#222222", "#00FF00", "#EEEEEE", "Lora", "Lora")));

        var palette = await new PaletteResolver(store).ResolveAsync("produto-sem-paleta", "financas");

        Assert.Equal("#222222", palette.Background);
    }

    [Fact]
    public async Task Sem_overrides_cai_no_catalogo_da_categoria()
    {
        var palette = await new PaletteResolver(new FakeFileStore()).ResolveAsync(null, "financas-autonomos");

        // catálogo determinístico de Finance (NicheStyleCatalog)
        Assert.Equal(PaletteCatalog.ForNiche("financas-autonomos").Background, palette.Background);
    }

    [Fact]
    public async Task Paleta_persistida_invalida_sem_background_e_ignorada()
    {
        var store = new FakeFileStore();
        await store.WriteTextAsync(ContentPaths.ProductPalette("p"), """{ "accent": "#FFF" }""");

        var palette = await new PaletteResolver(store).ResolveAsync("p", "saude");

        Assert.Equal(PaletteCatalog.ForNiche("saude").Background, palette.Background);
    }

    private static string Json(NichePalette p) => JsonSerializer.Serialize(p, new JsonSerializerOptions(JsonSerializerDefaults.Web));

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
