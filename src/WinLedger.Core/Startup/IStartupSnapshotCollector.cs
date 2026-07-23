using WinLedger.Domain.Startup;

namespace WinLedger.Core.Startup;

public interface IStartupSnapshotCollector
{
    Task<StartupSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken);
}
