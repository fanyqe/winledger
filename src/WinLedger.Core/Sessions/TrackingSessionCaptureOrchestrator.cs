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
    IFileSystemSnapshotStore fileSystemStore)
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

        var summaries = new List<TrackingSnapshotCaptureSummary>(subsystemPlan.Count);
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

                var summary = await CaptureSubsystemAsync(session.Id, request, subsystem, cancellationToken)
                    .ConfigureAwait(false);
                summaries.Add(summary);
                completedSubsystems++;

                progress?.Report(new TrackingSessionCaptureProgress(
                    request.Stage,
                    subsystem,
                    completedSubsystems,
                    subsystemPlan.Count,
                    "Captured"));
            }

            var updatedSession = session with { Status = StatusForCompletedStage(request.Stage) };
            await sessionStore.SaveSessionAsync(updatedSession, cancellationToken).ConfigureAwait(false);

            return new TrackingSessionCaptureResult(updatedSession, request.Stage, summaries);
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

    private async Task<TrackingSnapshotCaptureSummary> CaptureSubsystemAsync(
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

    private async Task<TrackingSnapshotCaptureSummary> CaptureRegistryAsync(
        Guid sessionId,
        TrackingSessionCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var targets = request.RegistryTargets
            ?? throw new InvalidOperationException("Registry targets are required for registry capture.");
        var snapshot = await registryCollector.CaptureAsync(sessionId, request.SnapshotName, targets, cancellationToken)
            .ConfigureAwait(false);
        await registryStore.SaveRegistrySnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(TrackingSubsystemKind.Registry, snapshot, snapshot.Keys.Count, snapshot.Warnings.Count);
    }

    private async Task<TrackingSnapshotCaptureSummary> CaptureServicesAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await serviceCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        await serviceStore.SaveServiceSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(TrackingSubsystemKind.Services, snapshot, snapshot.Services.Count, snapshot.Warnings.Count);
    }

    private async Task<TrackingSnapshotCaptureSummary> CaptureScheduledTasksAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await scheduledTaskCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        await scheduledTaskStore.SaveScheduledTaskSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(TrackingSubsystemKind.ScheduledTasks, snapshot, snapshot.Tasks.Count, snapshot.Warnings.Count);
    }

    private async Task<TrackingSnapshotCaptureSummary> CaptureStartupAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await startupCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        await startupStore.SaveStartupSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(TrackingSubsystemKind.Startup, snapshot, snapshot.Entries.Count, snapshot.Warnings.Count);
    }

    private async Task<TrackingSnapshotCaptureSummary> CaptureEnvironmentAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await environmentCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        await environmentStore.SaveEnvironmentSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(TrackingSubsystemKind.EnvironmentVariables, snapshot, snapshot.Variables.Count, snapshot.Warnings.Count);
    }

    private async Task<TrackingSnapshotCaptureSummary> CaptureHostsFileAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await hostsFileCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        await hostsFileStore.SaveHostsFileSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(TrackingSubsystemKind.HostsFile, snapshot, snapshot.Lines.Count, snapshot.Warnings.Count);
    }

    private async Task<TrackingSnapshotCaptureSummary> CaptureFirewallAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await firewallCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        await firewallStore.SaveFirewallSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(TrackingSubsystemKind.Firewall, snapshot, snapshot.Rules.Count, snapshot.Warnings.Count);
    }

    private async Task<TrackingSnapshotCaptureSummary> CaptureInstalledApplicationsAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var snapshot = await installedApplicationCollector.CaptureAsync(sessionId, snapshotName, cancellationToken)
            .ConfigureAwait(false);
        await installedApplicationStore.SaveInstalledApplicationsSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(
            TrackingSubsystemKind.InstalledApplications,
            snapshot,
            snapshot.Applications.Count,
            snapshot.Warnings.Count);
    }

    private async Task<TrackingSnapshotCaptureSummary> CaptureFileSystemAsync(
        Guid sessionId,
        TrackingSessionCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var options = request.FileSystemOptions
            ?? throw new InvalidOperationException("File system options are required for file-system capture.");
        var snapshot = await fileSystemCollector.CaptureAsync(sessionId, request.SnapshotName, options, cancellationToken)
            .ConfigureAwait(false);
        await fileSystemStore.SaveFileSystemSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return Summary(TrackingSubsystemKind.FileSystem, snapshot, snapshot.Entries.Count, snapshot.Warnings.Count);
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

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        RegistrySnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        ServiceSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        ScheduledTaskSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        StartupSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        EnvironmentSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        HostsFileSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        FirewallSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        InstalledApplicationsSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }

    private static TrackingSnapshotCaptureSummary Summary(
        TrackingSubsystemKind subsystem,
        FileSystemSnapshot snapshot,
        int itemCount,
        int warningCount)
    {
        return new TrackingSnapshotCaptureSummary(subsystem, snapshot.Id, snapshot.Name, snapshot.CapturedAt, itemCount, warningCount);
    }
}
