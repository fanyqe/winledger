using System.Text.Json;
using Microsoft.Data.Sqlite;
using WinLedger.Core.Sessions;
using WinLedger.Domain;
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
        var databasePath = CreateDatabasePath();
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
            FileSystemTestData.File("created.txt", hasRollbackData: true, content: "file")) with
        {
            ChangeJournalStates =
            [
                new FileSystemChangeJournalState(@"C:\", "NTFS", true, 123, 1, 10, 1, 100, null)
            ]
        };

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
        Assert.Equal((ulong)123, loadedFileSystemSnapshot?.ChangeJournalStates[0].JournalId);
    }

    [Fact]
    public async Task CommitCaptureAsyncRollsBackSessionAndSnapshotsTogether()
    {
        var databasePath = CreateDatabasePath();
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None);

        var session = Session("Atomic capture session", DateTimeOffset.UtcNow);
        await store.SaveSessionAsync(session, CancellationToken.None);

        var validSnapshot = RegistrySnapshot.Empty(session.Id, "Registry", DateTimeOffset.UtcNow);
        var mismatchedSnapshot = ServiceSnapshot.Empty(Guid.NewGuid(), "Services", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CommitCaptureAsync(
            session with { Status = TrackingSessionStatus.BaselineCaptured },
            [
                new TrackingSessionSnapshotCommit(TrackingSubsystemKind.Registry, validSnapshot),
                new TrackingSessionSnapshotCommit(TrackingSubsystemKind.Services, mismatchedSnapshot)
            ],
            CancellationToken.None));

        Assert.Null(await store.GetRegistrySnapshotAsync(validSnapshot.Id, CancellationToken.None));

        var loadedSession = await store.GetSessionAsync(session.Id, CancellationToken.None);
        Assert.Equal(TrackingSessionStatus.Created, loadedSession?.Status);
    }

    [Fact]
    public async Task InitializeAsyncRecordsAppliedMigration()
    {
        var databasePath = CreateDatabasePath();
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM schema_migrations WHERE version = 9;";

        var migrationName = (string?)await command.ExecuteScalarAsync();
        Assert.Equal("initial_schema", migrationName);
    }

    [Fact]
    public async Task SaveRegistrySnapshotAsyncRejectsMissingSession()
    {
        var databasePath = CreateDatabasePath();
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None);

        var snapshot = RegistrySnapshot.Empty(Guid.NewGuid(), "Orphan snapshot", DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<SqliteException>(
            () => store.SaveRegistrySnapshotAsync(snapshot, CancellationToken.None));

        Assert.Equal(19, exception.SqliteErrorCode);
    }

    [Fact]
    public async Task CleanupSessionsAsyncDryRunsAndDeletesOldSessionsWithSnapshots()
    {
        var databasePath = CreateDatabasePath();
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None);

        var deletedSession = Session("Old session to delete", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var retainedOldSession = Session("Old session to keep", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var retainedNewestSession = Session("Newest session", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        await store.SaveSessionAsync(deletedSession, CancellationToken.None);
        await store.SaveSessionAsync(retainedOldSession, CancellationToken.None);
        await store.SaveSessionAsync(retainedNewestSession, CancellationToken.None);

        var deletedRegistrySnapshot = RegistrySnapshot.Empty(deletedSession.Id, "Registry", DateTimeOffset.UtcNow);
        var retainedRegistrySnapshot = RegistrySnapshot.Empty(retainedOldSession.Id, "Registry", DateTimeOffset.UtcNow);
        var deletedFileSnapshot = FileSystemTestData.Snapshot(
            deletedSession.Id,
            FileSystemTestData.File("removed.txt", hasRollbackData: true, content: "removed"));
        await store.SaveRegistrySnapshotAsync(deletedRegistrySnapshot, CancellationToken.None);
        await store.SaveRegistrySnapshotAsync(retainedRegistrySnapshot, CancellationToken.None);
        await store.SaveFileSystemSnapshotAsync(deletedFileSnapshot, CancellationToken.None);

        var cutoff = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var dryRun = await store.CleanupSessionsAsync(
            cutoff,
            keepNewestSessions: 2,
            dryRun: true,
            cancellationToken: CancellationToken.None);

        Assert.True(dryRun.DryRun);
        Assert.Equal(1, dryRun.MatchedSessions);
        Assert.Equal(0, dryRun.DeletedSessions);
        Assert.Equal(1, dryRun.MatchedSnapshotRows["registry_snapshots"]);
        Assert.Equal(1, dryRun.MatchedSnapshotRows["file_system_snapshots"]);
        Assert.NotNull(await store.GetSessionAsync(deletedSession.Id, CancellationToken.None));
        Assert.Single(await store.ListRegistrySnapshotsAsync(deletedSession.Id, CancellationToken.None));
        Assert.Single(await store.ListFileSystemSnapshotsAsync(deletedSession.Id, CancellationToken.None));

        var cleanup = await store.CleanupSessionsAsync(
            cutoff,
            keepNewestSessions: 2,
            dryRun: false,
            cancellationToken: CancellationToken.None);

        Assert.False(cleanup.DryRun);
        Assert.Equal(1, cleanup.MatchedSessions);
        Assert.Equal(1, cleanup.DeletedSessions);
        Assert.Equal(1, cleanup.DeletedSnapshotRows["registry_snapshots"]);
        Assert.Equal(1, cleanup.DeletedSnapshotRows["file_system_snapshots"]);
        Assert.Null(await store.GetSessionAsync(deletedSession.Id, CancellationToken.None));
        Assert.Empty(await store.ListRegistrySnapshotsAsync(deletedSession.Id, CancellationToken.None));
        Assert.Empty(await store.ListFileSystemSnapshotsAsync(deletedSession.Id, CancellationToken.None));
        Assert.NotNull(await store.GetSessionAsync(retainedOldSession.Id, CancellationToken.None));
        Assert.NotNull(await store.GetSessionAsync(retainedNewestSession.Id, CancellationToken.None));
        Assert.Single(await store.ListRegistrySnapshotsAsync(retainedOldSession.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SaveRegistrySnapshotAsyncProtectsStoredSnapshotPayload()
    {
        var databasePath = CreateDatabasePath();
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None);

        var session = Session("Protected snapshot session", DateTimeOffset.UtcNow);
        await store.SaveSessionAsync(session, CancellationToken.None);

        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\ProtectedPayload");
        var snapshot = new RegistrySnapshot(
            Guid.NewGuid(),
            session.Id,
            "Protected",
            DateTimeOffset.UtcNow,
            [new RegistrySnapshotTarget(keyPath)],
            [new RegistryKeySnapshot(keyPath, [new RegistryValueSnapshot("Secret", RegistryValueType.String, "\"secret-value\"", "secret-value")])],
            []);

        await store.SaveRegistrySnapshotAsync(snapshot, CancellationToken.None);

        var storedPayload = await ReadSnapshotPayloadAsync(databasePath, "registry_snapshots", snapshot.Id);
        Assert.StartsWith("winledger-dpapi:v1:", storedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", storedPayload, StringComparison.Ordinal);

        var loaded = await store.GetRegistrySnapshotAsync(snapshot.Id, CancellationToken.None);
        Assert.Equal("secret-value", loaded?.Keys[0].Values[0].DisplayValue);
    }

    [Fact]
    public async Task GetRegistrySnapshotAsyncReadsLegacyPlaintextPayload()
    {
        var databasePath = CreateDatabasePath();
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None);

        var session = Session("Legacy snapshot session", DateTimeOffset.UtcNow);
        await store.SaveSessionAsync(session, CancellationToken.None);

        var snapshot = RegistrySnapshot.Empty(session.Id, "Legacy", DateTimeOffset.UtcNow);
        var payloadJson = JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options);
        await InsertSnapshotPayloadAsync(
            databasePath,
            "registry_snapshots",
            snapshot.Id,
            session.Id,
            snapshot.Name,
            snapshot.CapturedAt,
            payloadJson);

        var loaded = await store.GetRegistrySnapshotAsync(snapshot.Id, CancellationToken.None);

        Assert.Equal(snapshot.Id, loaded?.Id);
        Assert.Equal("Legacy", loaded?.Name);
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

    private static TrackingSession Session(string title, DateTimeOffset createdAt)
    {
        return new TrackingSession(
            Guid.NewGuid(),
            title,
            null,
            createdAt,
            "Windows",
            "X64",
            "redacted",
            false,
            TrackingSessionStatus.Created);
    }

    private static async Task<string> ReadSnapshotPayloadAsync(
        string databasePath,
        string tableName,
        Guid snapshotId)
    {
        await using var connection = CreateSqliteConnection(databasePath);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT payload_json FROM {tableName} WHERE id = $id;";
        command.Parameters.AddWithValue("$id", snapshotId.ToString());

        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    private static async Task InsertSnapshotPayloadAsync(
        string databasePath,
        string tableName,
        Guid snapshotId,
        Guid sessionId,
        string name,
        DateTimeOffset capturedAt,
        string payloadJson)
    {
        await using var connection = CreateSqliteConnection(databasePath);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            INSERT INTO {tableName}(id, session_id, name, captured_at_utc, payload_json)
            VALUES ($id, $sessionId, $name, $capturedAtUtc, $payloadJson);
            """;
        command.Parameters.AddWithValue("$id", snapshotId.ToString());
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$capturedAtUtc", capturedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$payloadJson", payloadJson);
        await command.ExecuteNonQueryAsync();
    }

    private static SqliteConnection CreateSqliteConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString());
    }

    private static string CreateDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), "WinLedgerTests", $"{Guid.NewGuid():N}.db");
    }
}
