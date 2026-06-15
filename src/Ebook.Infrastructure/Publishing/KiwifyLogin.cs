using Microsoft.Playwright;

namespace Ebook.Infrastructure.Publishing;

/// <summary>
/// Login interativo na Kiwify (E07): abre um navegador (headed), o operador faz login e a
/// sessão é salva em storageState para a automação reusar. Roda numa máquina com display:
/// <c>dotnet run --project src/Ebook.Api -- kiwify-login</c>.
/// </summary>
public static class KiwifyLogin
{
    public static async Task RunAsync(string baseUrl, string storageStatePath)
    {
        var dir = Path.GetDirectoryName(storageStatePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(baseUrl);

        Console.WriteLine("Faça login na Kiwify no navegador aberto e pressione ENTER aqui para salvar a sessão...");
        Console.ReadLine();

        await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = storageStatePath });
        Console.WriteLine($"Sessão Kiwify salva em {storageStatePath}");
    }
}
