using WinLedger.Domain.Services;

namespace WinLedger.Core.Services;

public interface IServiceSnapshotCollector
{
    Task<ServiceSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken);
}
