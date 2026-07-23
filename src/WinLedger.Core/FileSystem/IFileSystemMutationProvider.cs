using WinLedger.Domain.FileSystem;

namespace WinLedger.Core.FileSystem;

public interface IFileSystemMutationProvider
{
    Task<FileSystemEntrySnapshot?> ReadEntryAsync(
        string rootPath,
        string path,
        bool calculateHash,
        CancellationToken cancellationToken);

    Task DeleteEntryAsync(
        string rootPath,
        string path,
        FileSystemEntryKind kind,
        CancellationToken cancellationToken);

    Task RestoreFileContentAsync(
        string rootPath,
        string path,
        string contentBase64,
        DateTimeOffset? lastWriteTimeUtc,
        CancellationToken cancellationToken);
}
