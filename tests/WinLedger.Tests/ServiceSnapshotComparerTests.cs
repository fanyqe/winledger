using WinLedger.Comparison.Services;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Services;

namespace WinLedger.Tests;

public sealed class ServiceSnapshotComparerTests
{
    [Fact]
    public void CompareDetectsCreatedRemovedAndConfigurationChanges()
    {
        var sessionId = Guid.NewGuid();
        var beforeService = Service("ExampleService", "Example Service", ServiceStartModeKind.Automatic, @"C:\Before\service.exe", "LocalSystem", ServiceStateKind.Running, false, ["RpcSs"]);
        var removedService = Service("RemovedService", "Removed Service", ServiceStartModeKind.Manual, null, null, ServiceStateKind.Stopped, false, []);
        var afterService = Service("ExampleService", "Example Service Updated", ServiceStartModeKind.Disabled, @"C:\After\service.exe", "LocalService", ServiceStateKind.Stopped, true, ["RpcSs", "EventLog"]);
        var createdService = Service("CreatedService", "Created Service", ServiceStartModeKind.Automatic, @"C:\Created\service.exe", "LocalSystem", ServiceStateKind.Running, false, []);

        var baseline = Snapshot(sessionId, beforeService, removedService);
        var comparison = Snapshot(sessionId, afterService, createdService);

        var result = new ServiceSnapshotComparer().Compare(baseline, comparison, DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.ServiceCreated && change.ServiceName == "CreatedService");
        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.ServiceRemoved && change.ServiceName == "RemovedService");
        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.StartModeChanged && change.RollbackAvailability == RollbackAvailability.RequiresConfirmation);
        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.ExecutablePathChanged);
        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.DisplayNameChanged);
        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.ServiceAccountChanged);
        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.StateChanged && change.RollbackAvailability == RollbackAvailability.Unavailable);
        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.DelayedAutoStartChanged);
        Assert.Contains(result.Changes, change => change.Kind == ServiceChangeKind.DependenciesChanged);
    }

    [Fact]
    public void CompareRejectsSnapshotsFromDifferentSessions()
    {
        var baseline = Snapshot(Guid.NewGuid(), Service("ExampleService"));
        var comparison = Snapshot(Guid.NewGuid(), Service("ExampleService"));

        Assert.Throws<ArgumentException>(() => new ServiceSnapshotComparer().Compare(baseline, comparison, DateTimeOffset.UtcNow));
    }

    private static ServiceSnapshot Snapshot(Guid sessionId, params WindowsServiceSnapshot[] services)
    {
        return new ServiceSnapshot(Guid.NewGuid(), sessionId, "Snapshot", DateTimeOffset.UtcNow, services, []);
    }

    private static WindowsServiceSnapshot Service(
        string name,
        string? displayName = null,
        ServiceStartModeKind startMode = ServiceStartModeKind.Manual,
        string? executablePath = null,
        string? account = null,
        ServiceStateKind state = ServiceStateKind.Stopped,
        bool? delayedAutoStart = false,
        IReadOnlyList<string>? dependencies = null)
    {
        return new WindowsServiceSnapshot(
            name,
            displayName ?? name,
            startMode,
            executablePath,
            account,
            state,
            delayedAutoStart,
            dependencies ?? [],
            null);
    }
}
