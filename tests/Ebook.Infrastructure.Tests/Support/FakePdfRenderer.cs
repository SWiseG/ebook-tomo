using System.Text;
using Ebook.Application.Content.Pdf;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// Renderizador de PDF fake (sem Skia/native): devolve bytes válidos de PDF e registra
/// o último livro recebido. Mantém o teste de pipeline rápido e determinístico.
/// </summary>
public sealed class FakePdfRenderer : IPdfRenderer
{
    public PdfBook? Last { get; private set; }
    public bool LastHadCover { get; private set; }
    public int RenderCount { get; private set; }

    public byte[] Render(PdfBook book, byte[]? coverImage = null)
    {
        Last = book;
        LastHadCover = coverImage is { Length: > 0 };
        RenderCount++;
        return Encoding.UTF8.GetBytes($"%PDF-1.7\n% fake render of {book.Title}\n%%EOF");
    }
}
