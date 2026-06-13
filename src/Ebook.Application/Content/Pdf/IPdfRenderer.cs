namespace Ebook.Application.Content.Pdf;

/// <summary>Renderiza um <see cref="PdfBook"/> em bytes de PDF. Implementado na Infrastructure (QuestPDF).</summary>
public interface IPdfRenderer
{
    /// <param name="coverImage">Capa PNG (E09) usada como página de capa full-bleed; null = capa tipográfica.</param>
    byte[] Render(PdfBook book, byte[]? coverImage = null);
}
