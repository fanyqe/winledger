using WinLedger.Domain.FileSystem;

namespace WinLedger.Core.FileSystem;

public interface IFileSystemSnapshotCollector
{
    Task<FileSystemSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        FileSystemSnapshotOptions options,
        CancellationToken cancellationToken);
}
