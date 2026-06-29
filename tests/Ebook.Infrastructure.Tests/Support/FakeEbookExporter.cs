using System.Text;
using Ebook.Application.Content;
using Ebook.Application.Content.Pdf;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// Exportador EPUB fake: devolve bytes com header "PK" (ZIP) mas conteúdo mínimo.
/// Mantém o teste de pipeline rápido e determinístico.
/// </summary>
public sealed class FakeEbookExporter : IEbookExporter
{
    public PdfBook? Last { get; private set; }
    public int ExportCount { get; private set; }

    public byte[] ExportEpub(PdfBook book, byte[]? coverImage = null)
    {
        Last = book;
        ExportCount++;
        // PK\x03\x04 = ZIP local file header magic — mínimo aceitável para "bytes de EPUB"
        return Encoding.UTF8.GetBytes($"PK\x03\x04fake epub for {book.Title}");
    }
}
