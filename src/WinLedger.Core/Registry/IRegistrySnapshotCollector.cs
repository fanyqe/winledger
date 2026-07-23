using WinLedger.Domain.Registry;

namespace WinLedger.Core.Registry;

public interface IRegistrySnapshotCollector
{
    Task<RegistrySnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        IReadOnlyList<RegistrySnapshotTarget> targets,
        CancellationToken cancellationToken);
}
