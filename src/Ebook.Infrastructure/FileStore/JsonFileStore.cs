using System.Security.Cryptography;
using System.Text;
using Ebook.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.FileStore;

public sealed class DataOptions
{
    public const string SectionName = "Data";

    /// <summary>Raiz dos volumes de runtime (db, content, artifacts, logs).</summary>
    public string RootPath { get; set; } = "./data";
}

/// <summary>
/// FileStore em /data/content. Escrita atômica: grava em arquivo temporário
/// no mesmo diretório e move por cima (rename é atômico no mesmo volume).
/// </summary>
public sealed class JsonFileStore : IFileStore
{
    private readonly string _root;

    public JsonFileStore(IOptions<DataOptions> options)
    {
        _root = Path.GetFullPath(Path.Combine(options.Value.RootPath, "content"));
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> WriteTextAsync(string relativePath, string content, CancellationToken ct = default)
    {
        var fullPath = ResolveSafe(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var bytes = Encoding.UTF8.GetBytes(content);
        var tempPath = fullPath + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, ct);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new StoredFile(relativePath, hash, bytes.LongLength);
    }

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolveSafe(relativePath);
        return File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, Encoding.UTF8, ct) : null;
    }

    public bool Exists(string relativePath) => File.Exists(ResolveSafe(relativePath));

    private string ResolveSafe(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!fullPath.StartsWith(_root, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Caminho fora da raiz do FileStore: {relativePath}", nameof(relativePath));
        }

        return fullPath;
    }
}
