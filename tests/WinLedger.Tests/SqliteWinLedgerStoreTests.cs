using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.InstalledApplications;
using WinLedger.Domain.Registry;
using WinLedger.Domain.ScheduledTasks;
using WinLedger.Domain.Services;
using WinLedger.Domain.Sessions;
using WinLedger.Domain.Startup;
using WinLedger.Storage.Sqlite;

namespace WinLedger.Tests;

public sealed class SqliteWinLedgerStoreTests
{
    [Fact]
    public async Task StorePersistsSessionRegistryServiceScheduledTaskStartupEnvironmentHostsFileFirewallInstalledApplicationAndFileSystemSnapshots()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "WinLedgerTests", $"{Guid.NewGuid():N}.db");
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None);

        var sessionCreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var session = new TrackingSession(
            Guid.NewGuid(),
            "Test session",
            null,
            sessionCreatedAt,
            "Windows",
            "X64",
            "redacted",
            false,
            TrackingSessionStatus.Created);
        var olderSession = session with
        {
            Id = Guid.NewGuid(),
            Title = "Older session",
            CreatedAt = sessionCreatedAt.AddMinutes(-1)
        };
        var newerSession = session with
        {
            Id = Guid.NewGuid(),
            Title = "Newer session",
            CreatedAt = sessionCreatedAt.AddMinutes(1)
        };
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var snapshot = new RegistrySnapshot(
            Guid.NewGuid(),
            session.Id,
            "Baseline",
            DateTimeOffset.UtcNow,
            [new RegistrySnapshotTarget(keyPath)],
            [new RegistryKeySnapshot(keyPath, [new RegistryValueSnapshot("Setting", RegistryValueType.String, "\"value\"", "value")])],
            []);
        var serviceSnapshot = new ServiceSnapshot(
            Guid.NewGuid(),
            session.Id,
            "Services",
            DateTimeOffset.UtcNow,
            [new WindowsServiceSnapshot("ExampleService", "Example Service", ServiceStartModeKind.Automatic, @"C:\Example\service.exe", "LocalSystem", ServiceStateKind.Running, false, ["RpcSs"], null)],
            []);
        var taskSnapshot = new ScheduledTaskSnapshot(
            Guid.NewGuid(),
            session.Id,
            "Tasks",
            DateTimeOffset.UtcNow,
            [TaskSnapshot(@"\WinLedger\ExampleTask", enabled: true)],
            []);
        var startupSnapshot = new StartupSnapshot(
            Guid.NewGuid(),
            session.Id,
            "Startup",
            DateTimeOffset.UtcNow,
            [StartupEntry(@"C:\Startup\Example.lnk")],
            []);
        var environmentSnapshot = new EnvironmentSnapshot(
            Guid.NewGuid(),
            session.Id,
            "Environment",
            DateTimeOffset.UtcNow,
            [EnvironmentVariable("Path", @"C:\Windows;C:\Example\bin")],
            []);
        var hostsFileSnapshot = HostsFileTestData.Snapshot(
            session.Id,
            "Hosts",
            "127.0.0.1 localhost\r\n10.0.0.1 example.test\r\n");
        var firewallSnapshot = FirewallTestData.Snapshot(
            session.Id,
            FirewallTestData.Rule("Example firewall rule"));
        var installedApplicationSnapshot = InstalledApplicationTestData.Snapshot(
            session.Id,
            InstalledApplicationTestData.Application(
                "Example App",
                "{11111111-1111-1111-1111-111111111111}",
                source: InstalledApplicationSourceKind.MsiProduct,
                windowsInstaller: true),
            InstalledApplicationTestData.AppxPackage("Example Package"));
        var fileSystemSnapshot = FileSystemTestData.Snapshot(
            session.Id,
            FileSystemTestData.File("created.txt", hasRollbackData: true, content: "file"));

        await store.SaveSessionAsync(olderSession, CancellationToken.None);
        await store.SaveSessionAsync(session, CancellationToken.None);
        await store.SaveSessionAsync(newerSession, CancellationToken.None);
        await store.SaveRegistrySnapshotAsync(snapshot, CancellationToken.None);
        await store.SaveServiceSnapshotAsync(serviceSnapshot, CancellationToken.None);
        await store.SaveScheduledTaskSnapshotAsync(taskSnapshot, CancellationToken.None);
        await store.SaveStartupSnapshotAsync(startupSnapshot, CancellationToken.None);
        await store.SaveEnvironmentSnapshotAsync(environmentSnapshot, CancellationToken.None);
        await store.SaveHostsFileSnapshotAsync(hostsFileSnapshot, CancellationToken.None);
        await store.SaveFirewallSnapshotAsync(firewallSnapshot, CancellationToken.None);
        await store.SaveInstalledApplicationsSnapshotAsync(installedApplicationSnapshot, CancellationToken.None);
        await store.SaveFileSystemSnapshotAsync(fileSystemSnapshot, CancellationToken.None);

        var loadedSession = await store.GetSessionAsync(session.Id, CancellationToken.None);
        var loadedSessions = await store.ListSessionsAsync(CancellationToken.None);
        var loadedSnapshot = await store.GetRegistrySnapshotAsync(snapshot.Id, CancellationToken.None);
        var loadedServiceSnapshot = await store.GetServiceSnapshotAsync(serviceSnapshot.Id, CancellationToken.None);
        var loadedTaskSnapshot = await store.GetScheduledTaskSnapshotAsync(taskSnapshot.Id, CancellationToken.None);
        var loadedStartupSnapshot = await store.GetStartupSnapshotAsync(startupSnapshot.Id, CancellationToken.None);
        var loadedEnvironmentSnapshot = await store.GetEnvironmentSnapshotAsync(environmentSnapshot.Id, CancellationToken.None);
        var loadedHostsFileSnapshot = await store.GetHostsFileSnapshotAsync(hostsFileSnapshot.Id, CancellationToken.None);
        var loadedFirewallSnapshot = await store.GetFirewallSnapshotAsync(firewallSnapshot.Id, CancellationToken.None);
        var loadedInstalledApplicationSnapshot = await store.GetInstalledApplicationsSnapshotAsync(installedApplicationSnapshot.Id, CancellationToken.None);
        var loadedFileSystemSnapshot = await store.GetFileSystemSnapshotAsync(fileSystemSnapshot.Id, CancellationToken.None);

        Assert.Equal(session.Title, loadedSession?.Title);
        Assert.Equal(new[] { newerSession.Id, session.Id, olderSession.Id }, loadedSessions.Select(item => item.Id).ToArray());
        Assert.Equal(new[] { "Newer session", "Test session", "Older session" }, loadedSessions.Select(item => item.Title).ToArray());
        Assert.Equal(snapshot.Keys[0].Path.FullPath, loadedSnapshot?.Keys[0].Path.FullPath);
        Assert.Equal("Setting", loadedSnapshot?.Keys[0].Values[0].Name);
        Assert.Equal("ExampleService", loadedServiceSnapshot?.Services[0].Name);
        Assert.Equal(ServiceStartModeKind.Automatic, loadedServiceSnapshot?.Services[0].StartMode);
        Assert.Equal(@"\WinLedger\ExampleTask", loadedTaskSnapshot?.Tasks[0].FullPath);
        Assert.True(loadedTaskSnapshot?.Tasks[0].Enabled);
        Assert.Equal(@"C:\Startup\Example.lnk", loadedStartupSnapshot?.Entries[0].Location);
        Assert.Equal(StartupEntrySourceKind.StartupFolder, loadedStartupSnapshot?.Entries[0].Source);
        Assert.Equal("Path", loadedEnvironmentSnapshot?.Variables[0].Name);
        Assert.Equal(2, loadedEnvironmentSnapshot?.Variables[0].PathEntries.Count);
        Assert.True(loadedHostsFileSnapshot?.Exists);
        Assert.Equal(2, loadedHostsFileSnapshot?.Lines.Count);
        Assert.Equal(hostsFileSnapshot.ContentBase64, loadedHostsFileSnapshot?.ContentBase64);
        Assert.Equal("Example firewall rule", loadedFirewallSnapshot?.Rules[0].Name);
        Assert.Equal(2, loadedFirewallSnapshot?.Rules[0].Profiles);
        Assert.Equal("Example App", loadedInstalledApplicationSnapshot?.Applications[0].DisplayName);
        Assert.Equal(InstalledApplicationSourceKind.MsiProduct, loadedInstalledApplicationSnapshot?.Applications[0].Source);
        Assert.True(loadedInstalledApplicationSnapshot?.Applications[0].WindowsInstaller);
        Assert.Equal("Example Package", loadedInstalledApplicationSnapshot?.Applications[1].DisplayName);
        Assert.Equal(InstalledApplicationSourceKind.AppxPackage, loadedInstalledApplicationSnapshot?.Applications[1].Source);
        Assert.Equal("Example.Package_1.0.0.0_x64__publisherid", loadedInstalledApplicationSnapshot?.Applications[1].PackageFullName);
        Assert.Equal("Example.Package_publisherid", loadedInstalledApplicationSnapshot?.Applications[1].PackageFamilyName);
        Assert.Equal("created.txt", loadedFileSystemSnapshot?.Entries[0].RelativePath);
        Assert.Equal(FileSystemEntryKind.File, loadedFileSystemSnapshot?.Entries[0].Kind);
        Assert.True(loadedFileSystemSnapshot?.Entries[0].HasRollbackData);
    }

    private static ScheduledTaskDefinitionSnapshot TaskSnapshot(string path, bool enabled)
    {
        return new ScheduledTaskDefinitionSnapshot(
            path,
            @"\WinLedger",
            "ExampleTask",
            enabled,
            ScheduledTaskStateKind.Ready,
            "SYSTEM",
            ScheduledTaskPrivilegeLevelKind.HighestAvailable,
            [new ScheduledTaskActionSnapshot(ScheduledTaskActionKind.Execute, @"C:\Example\updater.exe", "--check", @"C:\Example", @"C:\Example\updater.exe --check")],
            [new ScheduledTaskTriggerSnapshot(ScheduledTaskTriggerKind.Logon, true, null, null, "Logon trigger enabled")],
            $"<Task><Enabled>{enabled}</Enabled></Task>");
    }

    private static StartupEntrySnapshot StartupEntry(string location)
    {
        return new StartupEntrySnapshot(
            $"StartupFolder|{location}",
            StartupEntrySourceKind.StartupFolder,
            Path.GetFileName(location),
            location,
            location,
            true,
            null,
            "Startup folder entry",
            "StartupFolder",
            42,
            DateTimeOffset.UnixEpoch);
    }

    private static EnvironmentVariableSnapshot EnvironmentVariable(string name, string value)
    {
        return new EnvironmentVariableSnapshot(
            EnvironmentVariableScopeKind.User,
            name,
            value,
            EnvironmentVariableValueType.ExpandString,
            value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            @"HKCU\Environment");
    }
}
