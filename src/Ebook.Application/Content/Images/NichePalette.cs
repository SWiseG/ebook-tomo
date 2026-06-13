using System.Text;

namespace Ebook.Application.Content.Images;

/// <summary>
/// Identidade visual de um nicho (E09-03): cores em hex e famílias tipográficas.
/// Serializável como config JSON por nicho; quando ausente, usa-se o catálogo padrão.
/// </summary>
public sealed record NichePalette(
    string Background,
    string Accent,
    string OnDark,
    string HeadingFont,
    string BodyFont);

/// <summary>Catálogo de paletas embutidas + seleção determinística por nicho.</summary>
public static class PaletteCatalog
{
    private static readonly NichePalette[] Palettes =
    [
        new("#1F2937", "#CBA15A", "#F9FAFB", "Times New Roman", "Times New Roman"), // clássico sóbrio
        new("#0F766E", "#5EEAD4", "#ECFEFF", "Arial", "Arial"),                       // moderno teal
        new("#7C2D12", "#FDBA74", "#FFF7ED", "Times New Roman", "Arial"),             // editorial quente
        new("#312E81", "#A5B4FC", "#EEF2FF", "Arial", "Arial")                        // índigo confiante
    ];

    public static NichePalette ForNiche(string slug)
    {
        var hash = 2166136261u; // FNV-1a, estável entre processos
        foreach (var b in Encoding.UTF8.GetBytes(slug ?? string.Empty))
        {
            hash = (hash ^ b) * 16777619u;
        }

        return Palettes[hash % (uint)Palettes.Length];
    }
}
