using Ebook.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.Tests.Ai;

public class PromptLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ebook-tests", Guid.NewGuid().ToString("N"));
    private readonly PromptLibrary _library;

    public PromptLibraryTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "ebook"));
        File.WriteAllText(Path.Combine(_root, "ebook", "outline.md"),
            "Gere outline para o nicho {{niche}} com {{chapters}} capítulos.");
        _library = new PromptLibrary(Options.Create(new AiOptions { PromptsPath = _root }));
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
    public async Task Renderiza_substituindo_variaveis()
    {
        var result = await _library.RenderAsync("ebook/outline", new Dictionary<string, string>
        {
            ["niche"] = "finanças",
            ["chapters"] = "8"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Gere outline para o nicho finanças com 8 capítulos.", result.Value);
    }

    [Fact]
    public async Task Variavel_faltando_retorna_erro()
    {
        var result = await _library.RenderAsync("ebook/outline", new Dictionary<string, string>
        {
            ["niche"] = "finanças"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.MissingVariable", result.Error.Code);
    }

    [Fact]
    public async Task Template_inexistente_retorna_erro()
    {
        var result = await _library.RenderAsync("nao/existe", new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.TemplateNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Hot_reload_detecta_alteracao_do_arquivo()
    {
        var path = Path.Combine(_root, "ebook", "outline.md");
        await _library.RenderAsync("ebook/outline", new Dictionary<string, string>
        {
            ["niche"] = "x", ["chapters"] = "1"
        });

        File.WriteAllText(path, "Novo template {{niche}}.");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));

        var result = await _library.RenderAsync("ebook/outline", new Dictionary<string, string> { ["niche"] = "y" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Novo template y.", result.Value);
    }
}
