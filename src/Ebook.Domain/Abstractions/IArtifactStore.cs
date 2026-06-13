namespace Ebook.Domain.Abstractions;

/// <summary>
/// Armazenamento de artefatos binários (/data/artifacts): PDF, imagens, vídeo, bundles de LP.
/// Escrita atômica (temp + rename) e hash SHA-256, espelhando o <see cref="IFileStore"/> de conteúdo.
/// </summary>
public interface IArtifactStore
{
    Task<StoredFile> WriteBytesAsync(string relativePath, byte[] content, CancellationToken ct = default);
    Task<byte[]?> ReadBytesAsync(string relativePath, CancellationToken ct = default);
    bool Exists(string relativePath);
}
