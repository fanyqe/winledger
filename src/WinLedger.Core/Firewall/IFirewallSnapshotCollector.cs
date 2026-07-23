using WinLedger.Domain.Firewall;

namespace WinLedger.Core.Firewall;

public interface IFirewallSnapshotCollector
{
    Task<FirewallSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken);
}
