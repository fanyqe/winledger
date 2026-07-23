using WinLedger.Core.Sessions;
using WinLedger.Domain.Firewall;

namespace WinLedger.Core.Firewall;

public interface IFirewallSnapshotStore : ITrackingSessionStore
{
    Task SaveFirewallSnapshotAsync(FirewallSnapshot snapshot, CancellationToken cancellationToken);

    Task<FirewallSnapshot?> GetFirewallSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FirewallSnapshot>> ListFirewallSnapshotsAsync(Guid sessionId, CancellationToken cancellationToken);
}
