using WinLedger.Core.Sessions;
using WinLedger.Domain.FileSystem;

namespace WinLedger.Core.FileSystem;

public interface IFileSystemSnapshotStore : ITrackingSessionStore
{
    Task SaveFileSystemSnapshotAsync(FileSystemSnapshot snapshot, CancellationToken cancellationToken);

    Task<FileSystemSnapshot?> GetFileSystemSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FileSystemSnapshot>> ListFileSystemSnapshotsAsync(Guid sessionId, CancellationToken cancellationToken);
}
