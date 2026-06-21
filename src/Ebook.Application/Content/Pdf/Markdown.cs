namespace Ebook.Application.Content.Pdf;

public enum MarkdownBlockKind
{
    Heading,
    Paragraph,
    Bullets,
    PullQuote,
    Callout,
    Timeline,  // passos numerados (lista ordenada "1.") → linha do tempo visual
    Stat,      // número de impacto: "> [!STAT] 97% | descrição"
    QuoteCard, // citação desenhada com ícone: "> [!FRASE] texto — autor"
    Image   // ilustração gerada por IA (Frente D): 1 por capítulo via IMediaGateway
}

/// <summary>Bloco estrutural de Markdown (nível para Heading, itens para Bullets, rótulo para Callout, bytes para Image).</summary>
public sealed record MarkdownBlock
{
    public required MarkdownBlockKind Kind { get; init; }
    public int Level { get; init; }
    public string Text { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<string> Items { get; init; } = [];
    public byte[]? ImageBytes { get; init; }   // preenchido para Kind=Image

    public static MarkdownBlock Heading(int level, string text) =>
        new() { Kind = MarkdownBlockKind.Heading, Level = level, Text = text };

    public static MarkdownBlock Paragraph(string text) =>
        new() { Kind = MarkdownBlockKind.Paragraph, Text = text };

    public static MarkdownBlock Bullets(IReadOnlyList<string> items) =>
        new() { Kind = MarkdownBlockKind.Bullets, Items = items };

    public static MarkdownBlock PullQuote(string text) =>
        new() { Kind = MarkdownBlockKind.PullQuote, Text = text };

    public static MarkdownBlock Callout(string label, string text) =>
        new() { Kind = MarkdownBlockKind.Callout, Label = label, Text = text };

    public static MarkdownBlock Image(byte[] bytes) =>
        new() { Kind = MarkdownBlockKind.Image, ImageBytes = bytes };

    public static MarkdownBlock Timeline(IReadOnlyList<string> steps) =>
        new() { Kind = MarkdownBlockKind.Timeline, Items = steps };

    public static MarkdownBlock Stat(string number, string label) =>
        new() { Kind = MarkdownBlockKind.Stat, Text = number, Label = label };

    public static MarkdownBlock QuoteCard(string text, string author) =>
        new() { Kind = MarkdownBlockKind.QuoteCard, Text = text, Label = author };
}

/// <summary>
/// Parser de Markdown leve e determinístico (sem dependências): headings #/##/###,
/// listas "- "/"* ", parágrafos e blocos de citação "> ". Uma citação simples vira pull quote;
/// com admoestação "> [!INSIGHT]" / "> [!CASO]" vira uma caixa de destaque (docs/11). Insumo do PDF.
/// </summary>
public static class MarkdownParser
{
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();
        var bullets = new List<string>();
        var quote = new List<string>();
        var steps = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(MarkdownBlock.Paragraph(string.Join(' ', paragraph)));
                paragraph.Clear();
            }
        }

        void FlushBullets()
        {
            if (bullets.Count > 0)
            {
                blocks.Add(MarkdownBlock.Bullets([.. bullets]));
                bullets.Clear();
            }
        }

        void FlushQuote()
        {
            if (quote.Count == 0)
            {
                return;
            }

            var first = quote[0];
            var type = "pull";
            string? label = null;
            if (first.StartsWith("[!INSIGHT]", StringComparison.OrdinalIgnoreCase))
            {
                type = "callout";
                label = "Insight rápido";
                first = first["[!INSIGHT]".Length..].Trim();
            }
            else if (first.StartsWith("[!CASO]", StringComparison.OrdinalIgnoreCase))
            {
                type = "callout";
                label = "Estudo de caso";
                first = first["[!CASO]".Length..].Trim();
            }
            else if (first.StartsWith("[!STAT]", StringComparison.OrdinalIgnoreCase))
            {
                type = "stat";
                first = first["[!STAT]".Length..].Trim();
            }
            else if (first.StartsWith("[!FRASE]", StringComparison.OrdinalIgnoreCase))
            {
                type = "quote";
                first = first["[!FRASE]".Length..].Trim();
            }

            var lines = new List<string>();
            if (first.Length > 0)
            {
                lines.Add(first);
            }

            for (var i = 1; i < quote.Count; i++)
            {
                lines.Add(quote[i]);
            }

            var text = string.Join(' ', lines);
            quote.Clear();

            switch (type)
            {
                case "callout":
                    blocks.Add(MarkdownBlock.Callout(label!, text));
                    break;
                case "stat":
                    var (number, statLabel) = SplitStat(text);
                    blocks.Add(MarkdownBlock.Stat(number, statLabel));
                    break;
                case "quote":
                    var (quoteText, author) = SplitAuthor(text);
                    blocks.Add(MarkdownBlock.QuoteCard(quoteText, author));
                    break;
                default:
                    blocks.Add(MarkdownBlock.PullQuote(text));
                    break;
            }
        }

        void FlushTimeline()
        {
            if (steps.Count > 0)
            {
                blocks.Add(MarkdownBlock.Timeline([.. steps]));
                steps.Clear();
            }
        }

        void FlushAll()
        {
            FlushParagraph();
            FlushBullets();
            FlushQuote();
            FlushTimeline();
        }

        foreach (var rawLine in (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushAll();
                continue;
            }

            if (TryHeading(line, out var level, out var headingText))
            {
                FlushAll();
                blocks.Add(MarkdownBlock.Heading(level, headingText));
                continue;
            }

            if (line.StartsWith(">", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushBullets();
                FlushTimeline();
                var content = line.Length > 1 && line[1] == ' ' ? line[2..] : line[1..];
                quote.Add(Clean(content));
                continue;
            }

            if (TryOrderedItem(line, out var stepText))
            {
                FlushParagraph();
                FlushBullets();
                FlushQuote();
                steps.Add(Clean(stepText));
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushQuote();
                FlushTimeline();
                bullets.Add(Clean(line[2..]));
                continue;
            }

            FlushBullets();
            FlushQuote();
            FlushTimeline();
            paragraph.Add(Clean(line));
        }

        FlushAll();
        return blocks;
    }

    private static bool TryHeading(string line, out int level, out string text)
    {
        level = 0;
        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        if (level is >= 1 and <= 3 && level < line.Length && line[level] == ' ')
        {
            text = Clean(line[(level + 1)..]);
            return true;
        }

        level = 0;
        text = string.Empty;
        return false;
    }

    /// <summary>Item de lista ordenada "1. " a "99. " → vira passo de timeline. Ignora números longos (ex.: anos).</summary>
    private static bool TryOrderedItem(string line, out string text)
    {
        text = string.Empty;
        var i = 0;
        while (i < line.Length && char.IsDigit(line[i]))
        {
            i++;
        }

        if (i is >= 1 and <= 2 && i + 1 < line.Length && line[i] == '.' && line[i + 1] == ' ')
        {
            text = line[(i + 2)..];
            return true;
        }

        return false;
    }

    /// <summary>"97% | descrição" → ("97%", "descrição"); sem barra → (texto, "").</summary>
    private static (string Number, string Label) SplitStat(string text)
    {
        var idx = text.IndexOf('|');
        return idx >= 0
            ? (text[..idx].Trim(), text[(idx + 1)..].Trim())
            : (text.Trim(), string.Empty);
    }

    /// <summary>"frase — autor" → ("frase", "autor"); sem travessão → (texto, "").</summary>
    private static (string Text, string Author) SplitAuthor(string text)
    {
        string[] seps = [" — ", " – ", " - "];
        foreach (var sep in seps)
        {
            var idx = text.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                return (text[..idx].Trim(), text[(idx + sep.Length)..].Trim());
            }
        }

        return (text.Trim(), string.Empty);
    }

    /// <summary>Remove marcadores inline (*, **, `) que o renderizador não interpreta.</summary>
    private static string Clean(string text) =>
        text.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Trim();
}
