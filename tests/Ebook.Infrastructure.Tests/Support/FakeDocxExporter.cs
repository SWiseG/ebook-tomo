using System.Text;
using Ebook.Application.Content;
using Ebook.Application.Content.Pdf;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// Exportador DOCX fake: devolve bytes mínimos com assinatura PK (ZIP/OpenXML).
/// Mantém o teste de pipeline rápido e determinístico.
/// </summary>
public sealed class FakeDocxExporter : IDocxExporter
{
    public PdfBook? Last { get; private set; }
    public int ExportCount { get; private set; }

    public byte[] ExportDocx(PdfBook book, byte[]? coverImage = null)
    {
        Last = book;
        ExportCount++;
        return Encoding.UTF8.GetBytes($"PK\x03\x04fake docx for {book.Title}");
    }
}
