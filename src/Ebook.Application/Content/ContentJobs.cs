namespace Ebook.Application.Content;

/// <summary>Tipos de job e chaves de idempotência naturais do pipeline de conteúdo.</summary>
public static class ContentJobs
{
    public const string Outline = "ebook.outline";
    public const string Chapter = "ebook.chapter";
    public const string Review = "ebook.review";
    public const string Cover = "ebook.cover";
    public const string Pdf = "ebook.pdf";

    public static string OutlineKey(Guid productId) => $"outline:{productId}";
    public static string ChapterKey(Guid productId, int n) => $"chapter:{productId}:{n}";
    public static string ReviewKey(Guid productId) => $"review:{productId}";
    public static string CoverKey(Guid productId) => $"cover:{productId}";
    public static string PdfKey(Guid productId) => $"pdf:{productId}";
}

public sealed record OutlineJobPayload(Guid ProductId);

public sealed record ChapterJobPayload(Guid ProductId, int ChapterNumber);

public sealed record ReviewJobPayload(Guid ProductId);

public sealed record CoverJobPayload(Guid ProductId);

public sealed record PdfJobPayload(Guid ProductId);
