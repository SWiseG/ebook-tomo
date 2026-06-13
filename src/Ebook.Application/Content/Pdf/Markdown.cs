namespace Ebook.Application.Content.Pdf;

public enum MarkdownBlockKind
{
    Heading,
    Paragraph,
    Bullets
}

/// <summary>Bloco estrutural de Markdown (nível para Heading, itens para Bullets).</summary>
public sealed record MarkdownBlock
{
    public required MarkdownBlockKind Kind { get; init; }
    public int Level { get; init; }
    public string Text { get; init; } = string.Empty;
    public IReadOnlyList<string> Items { get; init; } = [];

    public static MarkdownBlock Heading(int level, string text) =>
        new() { Kind = MarkdownBlockKind.Heading, Level = level, Text = text };

    public static MarkdownBlock Paragraph(string text) =>
        new() { Kind = MarkdownBlockKind.Paragraph, Text = text };

    public static MarkdownBlock Bullets(IReadOnlyList<string> items) =>
        new() { Kind = MarkdownBlockKind.Bullets, Items = items };
}

/// <summary>
/// Parser de Markdown leve e determinístico (sem dependências): headings #/##/###,
/// listas com "- "/"* " e parágrafos separados por linha em branco. Insumo do renderizador de PDF.
/// </summary>
public static class MarkdownParser
{
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();
        var bullets = new List<string>();

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

        foreach (var rawLine in (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                FlushBullets();
                continue;
            }

            if (TryHeading(line, out var level, out var headingText))
            {
                FlushParagraph();
                FlushBullets();
                blocks.Add(MarkdownBlock.Heading(level, headingText));
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                bullets.Add(Clean(line[2..]));
                continue;
            }

            FlushBullets();
            paragraph.Add(Clean(line.StartsWith("> ", StringComparison.Ordinal) ? line[2..] : line));
        }

        FlushParagraph();
        FlushBullets();
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

    /// <summary>Remove marcadores inline (*, **, `) que o renderizador não interpreta.</summary>
    private static string Clean(string text) =>
        text.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Trim();
}
