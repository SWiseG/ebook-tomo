using Ebook.Infrastructure.FileStore;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Tests.FileStore;

public class JsonFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ebook-tests", Guid.NewGuid().ToString("N"));
    private readonly JsonFileStore _store;

    public JsonFileStoreTests()
    {
        _store = new JsonFileStore(Options.Create(new DataOptions { RootPath = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Escreve_e_le_de_volta_com_hash_estavel()
    {
        var first = await _store.WriteTextAsync("niches/teste/pack.json", """{"a":1}""");
        var second = await _store.WriteTextAsync("niches/teste/pack2.json", """{"a":1}""");

        Assert.Equal(first.Sha256, second.Sha256); // hash depende só do conteúdo
        Assert.Equal("""{"a":1}""", await _store.ReadTextAsync("niches/teste/pack.json"));
        Assert.True(_store.Exists("niches/teste/pack.json"));
    }

    [Fact]
    public async Task Sobrescrita_e_atomica_sem_arquivos_temporarios_sobrando()
    {
        await _store.WriteTextAsync("a/b.json", "v1");
        await _store.WriteTextAsync("a/b.json", "v2");

        Assert.Equal("v2", await _store.ReadTextAsync("a/b.json"));
        var leftovers = Directory.GetFiles(Path.Combine(_root, "content", "a"), "*.tmp");
        Assert.Empty(leftovers);
    }

    [Fact]
    public async Task Ler_inexistente_retorna_null()
    {
        Assert.Null(await _store.ReadTextAsync("nao/existe.json"));
    }

    [Fact]
    public async Task Caminho_fora_da_raiz_e_rejeitado()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.WriteTextAsync("../escape.json", "x"));
    }
}
