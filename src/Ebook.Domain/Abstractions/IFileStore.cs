namespace Ebook.Domain.Abstractions;

public sealed record StoredFile(string RelativePath, string Sha256, long SizeBytes);

/// <summary>
/// Armazenamento de conteúdo em filesystem (/data/content). Escrita atômica
/// (temp + rename) e hash SHA-256 para indexação/cache.
/// </summary>
public interface IFileStore
{
    Task<StoredFile> WriteTextAsync(string relativePath, string content, CancellationToken ct = default);
    Task<string?> ReadTextAsync(string relativePath, CancellationToken ct = default);
    bool Exists(string relativePath);
}
