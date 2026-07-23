using WinLedger.Domain.InstalledApplications;

namespace WinLedger.Core.InstalledApplications;

public interface IInstalledApplicationSnapshotCollector
{
    Task<InstalledApplicationsSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken);
}
