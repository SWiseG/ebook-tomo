using Ebook.Application.Content.Pdf;

namespace Ebook.Application.Content;

/// <summary>
/// Exporta um <see cref="PdfBook"/> para o formato DOCX (OpenXML).
/// Implementado em Infrastructure via DocumentFormat.OpenXml.
/// </summary>
public interface IDocxExporter
{
    byte[] ExportDocx(PdfBook book, byte[]? coverImage = null);
}
