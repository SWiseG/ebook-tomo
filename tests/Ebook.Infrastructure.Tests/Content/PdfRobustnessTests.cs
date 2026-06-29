using System.Text;
using Ebook.Application.Content.Pdf;
using Ebook.Infrastructure.Content;
using SkiaSharp;

namespace Ebook.Infrastructure.Tests.Content;

/// <summary>
/// Robustez do render de PDF contra conteúdo adversarial de IA (docs/13). Cada bloco é exercitado
/// com o pior caso (palavra inquebrável, letra larga, número longo, imagem retrato/quadrada) — o
/// render NÃO pode estourar "conflicting size constraints". Inclui a rede de segurança (modo seguro).
/// </summary>
public class PdfRobustnessTests
{
    private static bool IsPdf(byte[] b) => b.Length > 800 && Encoding.ASCII.GetString(b[..4]) == "%PDF";

    private static byte[] SolidPng(int w, int h)
    {
        using var s = SKSurface.Create(new SKImageInfo(w, h));
        s.Canvas.Clear(SKColors.CornflowerBlue);
        using var img = s.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static PdfBook Book(params MarkdownBlock[] body) => new(
        "Título Normal do Ebook", "Subtítulo", "Promessa", PdfTheme.Modern,
        [MarkdownBlock.Heading(2, "Capítulo 1 — Abertura"), .. body],
        new PdfCta("Garanta", "https://x"));

    public static IEnumerable<object[]> Adversarial()
    {
        var w = new string('A', 120);                         // palavra inquebrável
        var url = "https://exemplo.com/" + new string('a', 150);
        yield return ["paragrafo-palavra", Book(MarkdownBlock.Paragraph($"Veja: {w} fim."))];
        yield return ["paragrafo-url", Book(MarkdownBlock.Paragraph($"Acesse {url} agora."))];
        yield return ["heading-palavra", Book(MarkdownBlock.Heading(3, w))];
        yield return ["bullet-palavra", Book(MarkdownBlock.Bullets([w, "normal"]))];
        yield return ["pullquote", Book(MarkdownBlock.PullQuote(w))];
        yield return ["callout", Book(MarkdownBlock.Callout("Insight rápido", w))];
        yield return ["stat-numero-longo", Book(MarkdownBlock.Stat(w, "rótulo"))];
        yield return ["quote", Book(MarkdownBlock.QuoteCard(w, w))];
        yield return ["timeline", Book(MarkdownBlock.Timeline([w, "passo"]))];
        yield return ["comparison", Book(MarkdownBlock.Comparison($"{w} | Depois", [$"{w} | {w}"]))];
        yield return ["dropcap-W", Book(MarkdownBlock.Paragraph($"W{w}"))];
        yield return ["cta-palavra", new PdfBook("T", "S", "P", PdfTheme.Modern,
            [MarkdownBlock.Paragraph("corpo")], new PdfCta(w, "https://x"))];
        yield return ["imagem-retrato", Book(MarkdownBlock.Image(SolidPng(512, 1024)))];
        yield return ["imagem-quadrada", Book(MarkdownBlock.Image(SolidPng(800, 800)))];
        yield return ["imagem-paisagem", Book(MarkdownBlock.Image(SolidPng(1024, 512)))];
    }

    [Theory]
    [MemberData(nameof(Adversarial))]
    public void Render_nao_estoura_com_bloco_adversarial(string name, PdfBook book)
    {
        _ = name;
        Assert.True(IsPdf(new QuestPdfRenderer().Render(book)));
    }

    [Fact]
    public void Modo_seguro_renderiza_todos_os_tipos_de_bloco_so_com_texto()
    {
        var book = Book(
            MarkdownBlock.Paragraph("Parágrafo."),
            MarkdownBlock.Heading(3, "Subtítulo"),
            MarkdownBlock.Bullets(["[ ] tarefa", "[x] feita", "item"]),
            MarkdownBlock.PullQuote("Citação de impacto."),
            MarkdownBlock.Callout("Insight rápido", "Texto do insight."),
            MarkdownBlock.Stat("97%", "dos casos"),
            MarkdownBlock.QuoteCard("Frase memorável.", "Autor"),
            MarkdownBlock.Timeline(["Passo 1", "Passo 2"]),
            MarkdownBlock.Comparison("Antes | Depois", ["ruim | bom"]),
            MarkdownBlock.Image(SolidPng(800, 800)),  // omitida no modo seguro
            MarkdownBlock.Divider());

        // chama o modo seguro diretamente (a rede de segurança do Render)
        Assert.True(IsPdf(new QuestPdfRenderer().RenderSafeMode(book)));
    }
}
