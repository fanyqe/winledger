using WinLedger.Comparison.EnvironmentVariables;
using WinLedger.Comparison.FileSystem;
using WinLedger.Comparison.Firewall;
using WinLedger.Comparison.Hosts;
using WinLedger.Comparison.InstalledApplications;
using WinLedger.Comparison.Registry;
using WinLedger.Comparison.ScheduledTasks;
using WinLedger.Comparison.Services;
using WinLedger.Comparison.Startup;
using WinLedger.Core.Abstractions;
using WinLedger.Core.EnvironmentVariables;
using WinLedger.Core.FileSystem;
using WinLedger.Core.Firewall;
using WinLedger.Core.Hosts;
using WinLedger.Core.InstalledApplications;
using WinLedger.Core.Registry;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Core.Services;
using WinLedger.Core.Sessions;
using WinLedger.Core.Startup;
using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Firewall;
using WinLedger.Domain.Hosts;
using WinLedger.Domain.InstalledApplications;
using WinLedger.Domain.Registry;
using WinLedger.Domain.ScheduledTasks;
using WinLedger.Domain.Services;
using WinLedger.Domain.Sessions;
using WinLedger.Domain.Startup;

namespace WinLedger.App.ViewModels;

public sealed class TrackingSessionReopenService(
    IClock clock,
    ITrackingSessionStore sessionStore,
    IRegistrySnapshotStore registryStore,
    RegistrySnapshotComparer registryComparer,
    IServiceSnapshotStore serviceStore,
    ServiceSnapshotComparer serviceComparer,
    IScheduledTaskSnapshotStore scheduledTaskStore,
    ScheduledTaskSnapshotComparer scheduledTaskComparer,
    IStartupSnapshotStore startupStore,
    StartupSnapshotComparer startupComparer,
    IEnvironmentSnapshotStore environmentStore,
    EnvironmentSnapshotComparer environmentComparer,
    IHostsFileSnapshotStore hostsFileStore,
    HostsFileSnapshotComparer hostsFileComparer,
    IFirewallSnapshotStore firewallStore,
    FirewallSnapshotComparer firewallComparer,
    IInstalledApplicationSnapshotStore installedApplicationStore,
    InstalledApplicationsSnapshotComparer installedApplicationComparer,
    IFileSystemSnapshotStore fileSystemStore,
    FileSystemSnapshotComparer fileSystemComparer)
{
    public async Task<IReadOnlyList<TrackingSessionListItem>> ListSessionsAsync(CancellationToken cancellationToken)
    {
        await sessionStore.InitializeAsync(cancellationToken).ConfigureAwait(true);
        var sessions = await sessionStore.ListSessionsAsync(cancellationToken).ConfigureAwait(true);
        return sessions.Select(session => new TrackingSessionListItem(session)).ToArray();
    }

    public async Task<LoadedTrackingSession> LoadAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await InitializeStoresAsync(cancellationToken).ConfigureAwait(true);

        var session = await sessionStore.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(true)
            ?? throw new InvalidOperationException($"Session was not found: {sessionId}");
        var comparedAt = clock.UtcNow;

        var registry = SelectPair(
            await registryStore.ListRegistrySnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);
        var services = SelectPair(
            await serviceStore.ListServiceSnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);
        var scheduledTasks = SelectPair(
            await scheduledTaskStore.ListScheduledTaskSnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);
        var startup = SelectPair(
            await startupStore.ListStartupSnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);
        var environment = SelectPair(
            await environmentStore.ListEnvironmentSnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);
        var hostsFile = SelectPair(
            await hostsFileStore.ListHostsFileSnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);
        var firewall = SelectPair(
            await firewallStore.ListFirewallSnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);
        var installedApplications = SelectPair(
            await installedApplicationStore.ListInstalledApplicationsSnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);
        var fileSystem = SelectPair(
            await fileSystemStore.ListFileSystemSnapshotsAsync(sessionId, cancellationToken).ConfigureAwait(true),
            snapshot => snapshot.Id,
            snapshot => snapshot.Name);

        return new LoadedTrackingSession(
            session,
            registry.Baseline,
            Compare(registry, (baseline, comparison) => registryComparer.Compare(baseline, comparison, comparedAt)),
            services.Baseline,
            Compare(services, (baseline, comparison) => serviceComparer.Compare(baseline, comparison, comparedAt)),
            scheduledTasks.Baseline,
            Compare(scheduledTasks, (baseline, comparison) => scheduledTaskComparer.Compare(baseline, comparison, comparedAt)),
            startup.Baseline,
            Compare(startup, (baseline, comparison) => startupComparer.Compare(baseline, comparison, comparedAt)),
            environment.Baseline,
            Compare(environment, (baseline, comparison) => environmentComparer.Compare(baseline, comparison, comparedAt)),
            hostsFile.Baseline,
            Compare(hostsFile, (baseline, comparison) => hostsFileComparer.Compare(baseline, comparison, comparedAt)),
            firewall.Baseline,
            Compare(firewall, (baseline, comparison) => firewallComparer.Compare(baseline, comparison, comparedAt)),
            installedApplications.Baseline,
            Compare(installedApplications, (baseline, comparison) => installedApplicationComparer.Compare(baseline, comparison, comparedAt)),
            fileSystem.Baseline,
            Compare(fileSystem, (baseline, comparison) => fileSystemComparer.Compare(baseline, comparison, comparedAt)));
    }

    private async Task InitializeStoresAsync(CancellationToken cancellationToken)
    {
        var stores = new HashSet<ITrackingSessionStore>(ReferenceEqualityComparer.Instance)
        {
            sessionStore,
            registryStore,
            serviceStore,
            scheduledTaskStore,
            startupStore,
            environmentStore,
            hostsFileStore,
            firewallStore,
            installedApplicationStore,
            fileSystemStore
        };

        foreach (var store in stores)
        {
            await store.InitializeAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private static TComparison? Compare<TSnapshot, TComparison>(
        SnapshotPair<TSnapshot> pair,
        Func<TSnapshot, TSnapshot, TComparison> comparer)
        where TSnapshot : class
    {
        return pair is { Baseline: { } baseline, Comparison: { } comparison } && pair.BaselineId != pair.ComparisonId
            ? comparer(baseline, comparison)
            : default;
    }

    private static SnapshotPair<TSnapshot> SelectPair<TSnapshot>(
        IReadOnlyList<TSnapshot> snapshots,
        Func<TSnapshot, Guid> getId,
        Func<TSnapshot, string> getName)
        where TSnapshot : class
    {
        var baseline = snapshots.LastOrDefault(snapshot =>
            string.Equals(getName(snapshot), "Baseline", StringComparison.OrdinalIgnoreCase))
            ?? snapshots.FirstOrDefault();
        var comparison = snapshots.LastOrDefault(snapshot =>
            string.Equals(getName(snapshot), "Comparison", StringComparison.OrdinalIgnoreCase));

        return new SnapshotPair<TSnapshot>(
            baseline,
            comparison,
            baseline is null ? null : getId(baseline),
            comparison is null ? null : getId(comparison));
    }

    private sealed record SnapshotPair<TSnapshot>(
        TSnapshot? Baseline,
        TSnapshot? Comparison,
        Guid? BaselineId,
        Guid? ComparisonId)
        where TSnapshot : class;
}
