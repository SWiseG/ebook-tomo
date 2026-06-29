namespace Ebook.Application.Content;

/// <summary>Tipos de job e chaves de idempotência naturais do pipeline de conteúdo.</summary>
public static class ContentJobs
{
    public const string Outline = "ebook.outline";
    public const string Chapter = "ebook.chapter";
    public const string Review = "ebook.review";
    public const string Cover = "ebook.cover";
    public const string Pdf = "ebook.pdf";
    public const string Lp = "lp.generate";
    public const string LpLab = "lp.lab";
    public const string Epub = "ebook.epub";
    public const string Docx = "ebook.docx";

    public static string OutlineKey(Guid productId) => $"outline:{productId}";
    public static string ChapterKey(Guid productId, int n) => $"chapter:{productId}:{n}";
    public static string ReviewKey(Guid productId) => $"review:{productId}";
    public static string ReviewRetryKey(Guid productId, int attempt) => $"review:{productId}:retry:{attempt}";
    public static string CoverKey(Guid productId) => $"cover:{productId}";
    public static string PdfKey(Guid productId) => $"pdf:{productId}";
    public static string LpKey(Guid productId) => $"lp:{productId}";
    public static string LpLabKey(Guid runId) => $"lp-lab:{runId}";
    public static string EpubKey(Guid productId) => $"epub:{productId}";
    public static string DocxKey(Guid productId) => $"docx:{productId}";
}

public sealed record OutlineJobPayload(Guid ProductId);

public sealed record ChapterJobPayload(Guid ProductId, int ChapterNumber);

public sealed record ReviewJobPayload(Guid ProductId, int RetryAttempt = 0);

public sealed record CoverJobPayload(Guid ProductId);

public sealed record PdfJobPayload(Guid ProductId);

public sealed record LpJobPayload(Guid ProductId);

public sealed record LpLabJobPayload(Guid RunId, Guid NicheId, string? Feedback);

public sealed record EpubJobPayload(Guid ProductId);

public sealed record DocxJobPayload(Guid ProductId);
