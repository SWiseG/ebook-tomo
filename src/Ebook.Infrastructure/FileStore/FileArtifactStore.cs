using System.Security.Cryptography;
using Ebook.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace Ebook.Infrastructure.FileStore;

/// <summary>
/// Artefatos binários em /data/artifacts. Escrita atômica: grava em arquivo temporário
/// no mesmo diretório e move por cima (rename atômico no mesmo volume).
/// </summary>
public sealed class FileArtifactStore : IArtifactStore
{
    private readonly string _root;

    public FileArtifactStore(IOptions<DataOptions> options)
    {
        _root = Path.GetFullPath(Path.Combine(options.Value.RootPath, "artifacts"));
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> WriteBytesAsync(string relativePath, byte[] content, CancellationToken ct = default)
    {
        var fullPath = ResolveSafe(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var tempPath = fullPath + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, content, ct);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        return new StoredFile(relativePath, hash, content.LongLength);
    }

    public async Task<byte[]?> ReadBytesAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolveSafe(relativePath);
        return File.Exists(fullPath) ? await File.ReadAllBytesAsync(fullPath, ct) : null;
    }

    public bool Exists(string relativePath) => File.Exists(ResolveSafe(relativePath));

    private string ResolveSafe(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!fullPath.StartsWith(_root, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Caminho fora da raiz de artefatos: {relativePath}", nameof(relativePath));
        }

        return fullPath;
    }
}
