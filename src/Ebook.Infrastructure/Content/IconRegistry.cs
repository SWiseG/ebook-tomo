namespace Ebook.Infrastructure.Content;

/// <summary>
/// Ícones SVG vetoriais embarcados (assets/icons/*.svg — Lucide, ISC) para enriquecer o PDF
/// (caixas de destaque, divisores). Guarda o TEXTO do SVG para permitir recolorir por nicho
/// (Lucide usa stroke="currentColor"). Idempotente; sem o diretório, degrada (sem ícone). Startup.
/// </summary>
public static class IconRegistry
{
    private static readonly Dictionary<string, string> Svgs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();
    private static bool _initialized;

    public static void Initialize(string iconsDirectory)
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            if (!Directory.Exists(iconsDirectory))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(iconsDirectory, "*.svg"))
            {
                try
                {
                    Svgs[Path.GetFileNameWithoutExtension(path)] = File.ReadAllText(path);
                }
                catch (Exception)
                {
                    // svg ilegível: ignora
                }
            }
        }
    }

    /// <summary>SVG do ícone recolorido (currentColor → hex), ou null se ausente (renderer omite o ícone).</summary>
    public static string? Colored(string name, string hex) =>
        Svgs.TryGetValue(name, out var svg)
            ? svg.Replace("currentColor", hex, StringComparison.Ordinal)
            : null;
}
