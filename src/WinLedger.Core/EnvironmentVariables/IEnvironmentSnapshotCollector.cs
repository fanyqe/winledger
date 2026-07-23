using WinLedger.Domain.EnvironmentVariables;

namespace WinLedger.Core.EnvironmentVariables;

public interface IEnvironmentSnapshotCollector
{
    Task<EnvironmentSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken);
}
