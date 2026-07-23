using WinLedger.Core.EnvironmentVariables;
using WinLedger.Core.FileSystem;
using WinLedger.Core.Firewall;
using WinLedger.Core.Hosts;
using WinLedger.Core.InstalledApplications;
using WinLedger.Core.Registry;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Core.Services;
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

namespace WinLedger.Core.Sessions;

public sealed class TrackingSessionCaptureOrchestrator(
    ITrackingSessionStore sessionStore,
    IRegistrySnapshotCollector registryCollector,
    IRegistrySnapshotStore registryStore,
    IServiceSnapshotCollector serviceCollector,
    IServiceSnapshotStore serviceStore,
    IScheduledTaskSnapshotCollector scheduledTaskCollector,
    IScheduledTaskSnapshotStore scheduledTaskStore,
    IStartupSnapshotCollector startupCollector,
    IStartupSnapshotStore startupStore,
    IEnvironmentSnapshotCollector environmentCollector,
    IEnvironmentSnapshotStore environmentStore,
    IHostsFileSnapshotCollector hostsFileCollector,
    IHostsFileSnapshotStore hostsFileStore,
    IFirewallSnapshotCollector firewallCollector,
    IFirewallSnapshotStore firewallStore,
    IInstalledApplicationSnapshotCollector installedApplicationCollector,
    IInstalledApplicationSnapshotStore installedApplicationStore,
    IFileSystemSnapshotCollector fileSystemCollector,
    IFileSystemSnapshotStore fileSystemStore,
    ITrackingSessionCaptureCommitStore? captureCommitStore = null)
{
    public async Task<TrackingSessionCaptureResult> CaptureAsync(
        TrackingSessionCaptureRequest request,
        IProgress<TrackingSessionCaptureProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subsystemPlan = ValidateAndNormalize(request);
        await InitializeStoresAsync(subsystemPlan, cancellationToken).ConfigureAwait(false);

        var session = await sessionStore.GetSessionAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Session was not found: {request.SessionId}");

        var capturedSnapshots = new List<CapturedSubsystemSnapshot>(subsystemPlan.Count);
        var completedSubsystems = 0;

        try
        {
            foreach (var subsystem in subsystemPlan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new TrackingSessionCaptureProgress(
                    request.Stage,
                    subsystem,
                    completedSubsystems,
                    subsystemPlan.Count,
                    "Starting"));

                var captured = await CaptureSubsystemAsync(session.Id, request, subsystem, cancellationToken)
                    .ConfigureAwait(false);
                capturedSnapshots.Add(captured);
                completedSubsystems++;

                progress?.Report(new TrackingSessionCaptureProgress(
                    request.Stage,
                    subsystem,
                    completedSubsystems,
                    subsystemPlan.Count,
                    "Captured"));
            }

            var updatedSession = session with { Status = StatusForCompletedStage(request.Stage) };
            await CommitCapturedSnapshotsAsync(updatedSession, capturedSnapshots, cancellationToken).ConfigureAwait(false);

            return new TrackingSessionCaptureResult(
                updatedSession,
                request.Stage,
                capturedSnapshots.Select(snapshot => snapshot.Summary).ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            await TryMarkSessionFailedAsync(session, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task InitializeStoresAsync(
        IReadOnlyList<TrackingSubsystemKind> subsystemPlan,
        CancellationToken cancellationToken)
    {
        var stores = new HashSet<ITrackingSessionStore>(ReferenceEqualityComparer.Instance) { sessionStore };
        foreach (var subsystem in subsystemPlan)
        {
            stores.Add(StoreFor(subsystem));
        }

        foreach (var store in stores)
        {
            await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<CapturedSubsystemSnapshot> CaptureSubsystemAsync(
        Guid sessionId,
        TrackingSessionCaptureRequest request,
        TrackingSubsystemKind subsystem,
        CancellationToken cancellationToken)
    {
        return subsystem switch
        {
            TrackingSubsystemKind.Registry => await CaptureRegistryAsync(sessionId, request, cancellationToken)
                .ConfigureAwait(false),
            TrackingSubsystemKind.Services => await CaptureServicesAsync(sessionId, request.SnapshotName, cancellationToken)
                .ConfigureAwait(false),
            TrackingSubsystemKind.ScheduledTasks => await CaptureScheduledTasksAsync(sessionId, request.SnapshotName, cancellationToken)
                .ConfigureAwait(false),
            TrackingSubsystemKind.Startup => await CaptureStartupAsync(sessionId, request.SnapshotName, cancellationToken)
                .ConfigureAwait(false),
            TrackingSubsystemKind.EnvironmentVariables => await CaptureEnvironmentAsync(sessionId, request.SnapshotName, cancellationToken)
                .ConfigureAwait(false),
            TrackingSubsystemKind.HostsFile => await CaptureHostsFileAsync(sessionId, request.SnapshotName, cancellationToken)
                .ConfigureAwait(false),
            TrackingSubsystemKind.Firewall => await CaptureFirewallAsync(sessionId, request.SnapshotName, cancellationToken)
                .ConfigureAwait(false),
            TrackingSubsystemKind.InstalledApplications => await CaptureInstalledApplicationsAsync(sessionId, request.SnapshotName, cancellationToken)
                .ConfigureAwait(false),
            TrackingSubsystemKind.FileSystem => await CaptureFileSystemAsync(sessionId, request, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(subsystem), subsystem, "Unsupported tracking subsystem.")
        };
    }

    private async Task<CapturedSubsystemSnapshot> CaptureRegistryAsync(
        Guid sessionId,
        TrackingSessionCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var targets = request.RegistryTargets
            ?? throw new InvalidOperationException("Registry targets are required for registry capture.");
        var snapshot = await registryCollector.CaptureAsync(sessionId, request.SnapshotName, targets, cancellationToken)
            .ConfigureAwait(false);
        return Captured(TrackingSubsystemKind.Registry, snapshot, snapshot.Keys.Count, snapshot.Warnings.Count);
    }

    private async Task<CapturedSubsystemSnapshot> CaptureServicesAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await serviceCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        return Captured(TrackingSubsystemKind.Services, snapshot, snapshot.Services.Count, snapshot.Warnings.Count);
    }

    private async Task<CapturedSubsystemSnapshot> CaptureScheduledTasksAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await scheduledTaskCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        return Captured(TrackingSubsystemKind.ScheduledTasks, snapshot, snapshot.Tasks.Count, snapshot.Warnings.Count);
    }

    private async Task<CapturedSubsystemSnapshot> CaptureStartupAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await startupCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        return Captured(TrackingSubsystemKind.Startup, snapshot, snapshot.Entries.Count, snapshot.Warnings.Count);
    }

    private async Task<CapturedSubsystemSnapshot> CaptureEnvironmentAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await environmentCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        return Captured(TrackingSubsystemKind.EnvironmentVariables, snapshot, snapshot.Variables.Count, snapshot.Warnings.Count);
    }

    private async Task<CapturedSubsystemSnapshot> CaptureHostsFileAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await hostsFileCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        return Captured(TrackingSubsystemKind.HostsFile, snapshot, snapshot.Lines.Count, snapshot.Warnings.Count);
    }

    private async Task<CapturedSubsystemSnapshot> CaptureFirewallAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await firewallCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        return Captured(TrackingSubsystemKind.Firewall, snapshot, snapshot.Rules.Count, snapshot.Warnings.Count);
    }

    private async Task<CapturedSubsystemSnapshot> CaptureInstalledApplicationsAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await installedApplicationCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        return Captured(
            TrackingSubsystemKind.InstalledApplications,
            snapshot,
            snapshot.Applications.Count,
            snapshot.Warnings.Count);
    }

    private async Task<CapturedSubsystemSnapshot> CaptureFileSystemAsync(
        Guid sessionId,
        TrackingSessionCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var options = request.FileSystemOptions
            ?? throw new InvalidOperationException("File system options are required for file-system capture.");
        var snapshot = await fileSystemCollector.CaptureAsync(sessionId, request.SnapshotName, options, cancellationToken)
            .ConfigureAwait(false);
        return Captured(TrackingSubsystemKind.FileSystem, snapshot, snapshot.Entries.Count, snapshot.Warnings.Count);
    }

    private async Task CommitCapturedSnapshotsAsync(
        TrackingSession updatedSession,
        IReadOnlyList<CapturedSubsystemSnapshot> capturedSnapshots,
        CancellationToken cancellationToken)
    {
        if (captureCommitStore is not null)
        {
            await captureCommitStore.CommitCaptureAsync(
                updatedSession,
                capturedSnapshots.Select(snapshot => snapshot.Commit).ToArray(),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var snapshot in capturedSnapshots)
        {
            await SaveCapturedSnapshotAsync(snapshot.Commit, cancellationToken).ConfigureAwait(false);
        }

        await sessionStore.SaveSessionAsync(updatedSession, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveCapturedSnapshotAsync(
        TrackingSessionSnapshotCommit commit,
        CancellationToken cancellationToken)
    {
        switch (commit.Subsystem, commit.Snapshot)
        {
            case (TrackingSubsystemKind.Registry, RegistrySnapshot snapshot):
                await registryStore.SaveRegistrySnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            case (TrackingSubsystemKind.Services, ServiceSnapshot snapshot):
                await serviceStore.SaveServiceSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            case (TrackingSubsystemKind.ScheduledTasks, ScheduledTaskSnapshot snapshot):
                await scheduledTaskStore.SaveScheduledTaskSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            case (TrackingSubsystemKind.Startup, StartupSnapshot snapshot):
                await startupStore.SaveStartupSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            case (TrackingSubsystemKind.EnvironmentVariables, EnvironmentSnapshot snapshot):
                await environmentStore.SaveEnvironmentSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            case (TrackingSubsystemKind.HostsFile, HostsFileSnapshot snapshot):
                await hostsFileStore.SaveHostsFileSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            case (TrackingSubsystemKind.Firewall, FirewallSnapshot snapshot):
                await firewallStore.SaveFirewallSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            case (TrackingSubsystemKind.InstalledApplications, InstalledApplicationsSnapshot snapshot):
                await installedApplicationStore.SaveInstalledApplicationsSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            case (TrackingSubsystemKind.FileSystem, FileSystemSnapshot snapshot):
                await fileSystemStore.SaveFileSystemSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported snapshot commit payload for {commit.Subsystem}.");
        }
    }

    private ITrackingSessionStore StoreFor(TrackingSubsystemKind subsystem)
    {
        return subsystem switch
        {
            TrackingSubsystemKind.Registry => registryStore,
            TrackingSubsystemKind.Services => serviceStore,
            TrackingSubsystemKind.ScheduledTasks => scheduledTaskStore,
            TrackingSubsystemKind.Startup => startupStore,
            TrackingSubsystemKind.EnvironmentVariables => environmentStore,
            TrackingSubsystemKind.HostsFile => hostsFileStore,
            TrackingSubsystemKind.Firewall => firewallStore,
            TrackingSubsystemKind.InstalledApplications => installedApplicationStore,
            TrackingSubsystemKind.FileSystem => fileSystemStore,
            _ => throw new ArgumentOutOfRangeException(nameof(subsystem), subsystem, "Unsupported tracking subsystem.")
        };
    }

    private async Task TryMarkSessionFailedAsync(TrackingSession session, CancellationToken cancellationToken)
    {
        try
        {
            await sessionStore.SaveSessionAsync(session with { Status = TrackingSessionStatus.Failed }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // The capture failure is more important than a best-effort status update failure.
        }
    }

    private static TrackingSessionStatus StatusForCompletedStage(TrackingSnapshotStage stage)
    {
        return stage switch
        {
            TrackingSnapshotStage.Baseline => TrackingSessionStatus.BaselineCaptured,
            TrackingSnapshotStage.Comparison => TrackingSessionStatus.ComparisonCaptured,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported snapshot stage.")
        };
    }

    private static IReadOnlyList<TrackingSubsystemKind> ValidateAndNormalize(TrackingSessionCaptureRequest request)
    {
        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SnapshotName))
        {
            throw new ArgumentException("Snapshot name is required.", nameof(request));
        }

        if (request.Subsystems.Count == 0)
        {
            throw new ArgumentException("At least one tracking subsystem is required.", nameof(request));
        }

        var selected = new List<TrackingSubsystemKind>();
        foreach (var subsystem in request.Subsystems)
        {
            if (!Enum.IsDefined(subsystem))
            {
                throw new ArgumentException($"Unsupported tracking subsystem: {subsystem}.", nameof(request));
            }

            if (!selected.Contains(subsystem))
            {
                selected.Add(subsystem);
            }
        }

        if (selected.Contains(TrackingSubsystemKind.Registry) && request.RegistryTargets?.Count is not > 0)
        {
            throw new ArgumentException("Registry capture requires at least one registry target.", nameof(request));
        }

        if (selected.Contains(TrackingSubsystemKind.FileSystem) && request.FileSystemOptions?.MonitoredRoots.Count is not > 0)
        {
            throw new ArgumentException("File-system capture requires at least one monitored root.", nameof(request));
        }

        return selected;
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        RegistrySnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        ServiceSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        ScheduledTaskSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        StartupSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        EnvironmentSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        HostsFileSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        FirewallSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        InstalledApplicationsSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot Captured(
        TrackingSubsystemKind subsystem,
        FileSystemSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return CreateCapturedSnapshot(subsystem, snapshot, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static CapturedSubsystemSnapshot CreateCapturedSnapshot(
        TrackingSubsystemKind subsystem,
        object snapshot,
        Guid snapshotId,
        string snapshotName,
        DateTimeOffset capturedAt,
        int itemCount,
        int warningCount)
    {
        return new CapturedSubsystemSnapshot(
            new TrackingSessionSnapshotCommit(subsystem, snapshot),
            new TrackingSnapshotCaptureSummary(subsystem, snapshotId, snapshotName, capturedAt, itemCount, warningCount));
    }

    private sealed record CapturedSubsystemSnapshot(
        TrackingSessionSnapshotCommit Commit,
        TrackingSnapshotCaptureSummary Summary);
}
