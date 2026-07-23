using Microsoft.Data.Sqlite;
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
using WinLedger.Storage.Sqlite;

namespace WinLedger.Tests;

public sealed class TrackingSessionCaptureOrchestratorTests
{
    [Fact]
    public async Task CaptureAsyncStoresSelectedSnapshotsAndMarksBaselineCaptured()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new SqliteWinLedgerStore(Path.Combine(directory, "winledger.db"));
            var session = CreateSession();
            await store.InitializeAsync(CancellationToken.None);
            await store.SaveSessionAsync(session, CancellationToken.None);

            var collectors = new FakeCollectors();
            var orchestrator = CreateOrchestrator(store, collectors);
            var registryTargets = new[]
            {
                new RegistrySnapshotTarget(RegistryPath.Parse(@"HKCU\Software\WinLedgerTest"), IncludeSubKeys: true),
                new RegistrySnapshotTarget(RegistryPath.Parse(@"HKCU\Software\WinLedgerTest2"), IncludeSubKeys: true)
            };
            var fileOptions = FileSystemSnapshotOptions.ForRoots(directory);
            var progressEvents = new List<TrackingSessionCaptureProgress>();

            var result = await orchestrator.CaptureAsync(
                new TrackingSessionCaptureRequest(
                    session.Id,
                    "Baseline",
                    TrackingSnapshotStage.Baseline,
                    [TrackingSubsystemKind.Registry, TrackingSubsystemKind.Services, TrackingSubsystemKind.FileSystem],
                    registryTargets,
                    fileOptions),
                new ImmediateProgress<TrackingSessionCaptureProgress>(progressEvents.Add),
                CancellationToken.None);

            Assert.Equal(TrackingSessionStatus.BaselineCaptured, result.Session.Status);
            Assert.Equal(
                [TrackingSubsystemKind.Registry, TrackingSubsystemKind.Services, TrackingSubsystemKind.FileSystem],
                result.Snapshots.Select(snapshot => snapshot.Subsystem).ToArray());
            Assert.Equal(registryTargets, collectors.RegistryTargets);
            Assert.Same(fileOptions, collectors.FileSystemOptions);
            Assert.Equal(6, progressEvents.Count);
            Assert.All(result.Snapshots, snapshot => Assert.Equal("Baseline", snapshot.SnapshotName));
            Assert.Single(await store.ListRegistrySnapshotsAsync(session.Id, CancellationToken.None));
            Assert.Single(await store.ListServiceSnapshotsAsync(session.Id, CancellationToken.None));
            Assert.Single(await store.ListFileSystemSnapshotsAsync(session.Id, CancellationToken.None));

            var storedSession = await store.GetSessionAsync(session.Id, CancellationToken.None);
            Assert.Equal(TrackingSessionStatus.BaselineCaptured, storedSession?.Status);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public async Task CaptureAsyncStopsBeforeNextSubsystemWhenCanceled()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new SqliteWinLedgerStore(Path.Combine(directory, "winledger.db"));
            var session = CreateSession();
            await store.InitializeAsync(CancellationToken.None);
            await store.SaveSessionAsync(session, CancellationToken.None);

            var orchestrator = CreateOrchestrator(store, new FakeCollectors());
            using var cancellation = new CancellationTokenSource();
            var progress = new ImmediateProgress<TrackingSessionCaptureProgress>(progress =>
            {
                if (progress is { Subsystem: TrackingSubsystemKind.Services, Message: "Captured" })
                {
                    cancellation.Cancel();
                }
            });

            await Assert.ThrowsAsync<OperationCanceledException>(() => orchestrator.CaptureAsync(
                new TrackingSessionCaptureRequest(
                    session.Id,
                    "Baseline",
                    TrackingSnapshotStage.Baseline,
                    [TrackingSubsystemKind.Services, TrackingSubsystemKind.EnvironmentVariables],
                    null,
                    null),
                progress,
                cancellation.Token));

            Assert.Empty(await store.ListServiceSnapshotsAsync(session.Id, CancellationToken.None));
            Assert.Empty(await store.ListEnvironmentSnapshotsAsync(session.Id, CancellationToken.None));

            var storedSession = await store.GetSessionAsync(session.Id, CancellationToken.None);
            Assert.Equal(TrackingSessionStatus.Created, storedSession?.Status);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static TrackingSessionCaptureOrchestrator CreateOrchestrator(
        SqliteWinLedgerStore store,
        FakeCollectors collectors)
    {
        return new TrackingSessionCaptureOrchestrator(
            store,
            collectors,
            store,
            collectors,
            store,
            collectors,
            store,
            collectors,
            store,
            collectors,
            store,
            collectors,
            store,
            collectors,
            store,
            collectors,
            store,
            collectors,
            store,
            store);
    }

    private static TrackingSession CreateSession()
    {
        return new TrackingSession(
            Guid.NewGuid(),
            "Test session",
            null,
            DateTimeOffset.UtcNow,
            "Windows",
            "X64",
            "sid-hash",
            false,
            TrackingSessionStatus.Created);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WinLedgerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTempDirectory(string directory)
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
        }
    }

    private sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value)
        {
            report(value);
        }
    }

    private sealed class FakeCollectors :
        IRegistrySnapshotCollector,
        IServiceSnapshotCollector,
        IScheduledTaskSnapshotCollector,
        IStartupSnapshotCollector,
        IEnvironmentSnapshotCollector,
        IHostsFileSnapshotCollector,
        IFirewallSnapshotCollector,
        IInstalledApplicationSnapshotCollector,
        IFileSystemSnapshotCollector
    {
        public IReadOnlyList<RegistrySnapshotTarget>? RegistryTargets { get; private set; }

        public FileSystemSnapshotOptions? FileSystemOptions { get; private set; }

        Task<RegistrySnapshot> IRegistrySnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            IReadOnlyList<RegistrySnapshotTarget> targets,
            CancellationToken cancellationToken)
        {
            RegistryTargets = targets.ToArray();
            return Task.FromResult(new RegistrySnapshot(
                Guid.NewGuid(),
                sessionId,
                snapshotName,
                DateTimeOffset.UtcNow,
                RegistryTargets,
                [],
                []));
        }

        Task<ServiceSnapshot> IServiceSnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ServiceSnapshot.Empty(sessionId, snapshotName, DateTimeOffset.UtcNow));
        }

        Task<ScheduledTaskSnapshot> IScheduledTaskSnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ScheduledTaskSnapshot.Empty(sessionId, snapshotName, DateTimeOffset.UtcNow));
        }

        Task<StartupSnapshot> IStartupSnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(StartupSnapshot.Empty(sessionId, snapshotName, DateTimeOffset.UtcNow));
        }

        Task<EnvironmentSnapshot> IEnvironmentSnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(EnvironmentSnapshot.Empty(sessionId, snapshotName, DateTimeOffset.UtcNow));
        }

        Task<HostsFileSnapshot> IHostsFileSnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(HostsFileSnapshot.Missing(sessionId, snapshotName, DateTimeOffset.UtcNow, "hosts"));
        }

        Task<FirewallSnapshot> IFirewallSnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new FirewallSnapshot(
                Guid.NewGuid(),
                sessionId,
                snapshotName,
                DateTimeOffset.UtcNow,
                [],
                []));
        }

        Task<InstalledApplicationsSnapshot> IInstalledApplicationSnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(InstalledApplicationsSnapshot.Empty(sessionId, snapshotName, DateTimeOffset.UtcNow));
        }

        Task<FileSystemSnapshot> IFileSystemSnapshotCollector.CaptureAsync(
            Guid sessionId,
            string snapshotName,
            FileSystemSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            FileSystemOptions = options;
            return Task.FromResult(new FileSystemSnapshot(
                Guid.NewGuid(),
                sessionId,
                snapshotName,
                DateTimeOffset.UtcNow,
                options,
                [],
                []));
        }
    }
}
