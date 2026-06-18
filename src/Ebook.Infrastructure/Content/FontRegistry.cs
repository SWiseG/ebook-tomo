using System.Text.RegularExpressions;
using QuestPDF.Drawing;
using SkiaSharp;

namespace Ebook.Infrastructure.Content;

/// <summary>
/// Registra as fontes embarcadas (assets/fonts/&lt;Familia&gt;-&lt;Peso&gt;.ttf) nos dois motores:
/// QuestPDF (PDF) e SkiaSharp (capa/cards). A família e o peso são derivados do NOME DO ARQUIVO
/// (não dos nomes internos do .ttf, que variam por eixo óptico), garantindo que o catálogo de
/// estilo (ex.: "Merriweather", "Playfair Display") sempre case. Idempotente; sem os arquivos,
/// degrada graciosamente (os renderizadores caem no fallback). Chamado uma vez no startup.
/// </summary>
public static partial class FontRegistry
{
    // chave: "familia|bold" — distingue Regular(400) de Bold(700) para o Skia (capa/cards)
    private static readonly Dictionary<string, SKTypeface> Typefaces = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();
    private static bool _initialized;

    private static string Key(string family, bool bold) => $"{family}|{(bold ? "b" : "r")}";

    public static void Initialize(string fontsDirectory)
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            if (!Directory.Exists(fontsDirectory))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(fontsDirectory, "*.ttf"))
            {
                try
                {
                    var (family, bold) = ParseName(Path.GetFileNameWithoutExtension(path));

                    using (var stream = File.OpenRead(path))
                    {
                        // nome customizado: QuestPDF passa a conhecer a fonte pelo nome do catálogo
                        FontManager.RegisterFontWithCustomName(family, stream);
                    }

                    var typeface = SKTypeface.FromFile(path);
                    if (typeface is not null)
                    {
                        Typefaces[Key(family, bold)] = typeface;
                    }
                }
                catch (Exception)
                {
                    // fonte inválida/ilegível: ignora e mantém o fallback dos renderizadores
                }
            }
        }
    }

    /// <summary>Typeface embarcado para o Skia (família + peso), ou null (composer cai em FromFamilyName/Default).</summary>
    public static SKTypeface? Resolve(string family, bool bold = false) =>
        Typefaces.TryGetValue(Key(family, bold), out var typeface) ? typeface
        : Typefaces.TryGetValue(Key(family, !bold), out var fallback) ? fallback // peso alternativo, se só um existir
        : null;

    // "Merriweather-Bold" -> ("Merriweather", true) · "PlayfairDisplay-Regular" -> ("Playfair Display", false)
    private static (string Family, bool Bold) ParseName(string fileBaseName)
    {
        var dash = fileBaseName.LastIndexOf('-');
        var rawName = dash > 0 ? fileBaseName[..dash] : fileBaseName;
        var weight = dash > 0 ? fileBaseName[(dash + 1)..] : "Regular";
        var family = CamelBoundary().Replace(rawName, " "); // PlayfairDisplay -> "Playfair Display"
        return (family, weight.Equals("Bold", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex CamelBoundary();
}
