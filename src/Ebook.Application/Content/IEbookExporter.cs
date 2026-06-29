using Ebook.Application.Content.Pdf;

namespace Ebook.Application.Content;

/// <summary>
/// Exporta um <see cref="PdfBook"/> para o formato EPUB 3.
/// Implementado em Infrastructure via System.IO.Compression (sem deps nativas).
/// </summary>
public interface IEbookExporter
{
    byte[] ExportEpub(PdfBook book, byte[]? coverImage = null);
}
