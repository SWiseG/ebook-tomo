using Ebook.Domain.Common;

namespace Ebook.Application.Content;

public static class ContentErrors
{
    public static Error NicheNotFound(Guid id) =>
        new("Content.Niche.NotFound", $"Nicho {id} não encontrado.");

    public static Error ProductNotFound(Guid id) =>
        new("Content.Product.NotFound", $"Produto {id} não encontrado.");

    public static Error OutlineMissing(string slug) =>
        new("Content.OutlineMissing", $"Outline ausente para o produto '{slug}'.");

    public static Error ChapterNotInOutline(int n) =>
        new("Content.ChapterNotInOutline", $"Capítulo {n} não existe no outline.");

    public static Error ManuscriptMissing(string slug) =>
        new("Content.ManuscriptMissing", $"Manuscrito ainda não gerado para o produto '{slug}'.");

    public static Error PdfMissing(string slug) =>
        new("Content.PdfMissing", $"PDF ainda não gerado para o produto '{slug}'.");

    public static Error CoverMissing(string slug) =>
        new("Content.CoverMissing", $"Capa ainda não gerada para o produto '{slug}'.");
}
