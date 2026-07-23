using System.Text.Json;
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
using WinLedger.Domain;
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

namespace WinLedger.Storage.Sqlite;

public sealed class SqliteWinLedgerStore(string databasePath) : ITrackingSessionStore, IRegistrySnapshotStore, IServiceSnapshotStore, IScheduledTaskSnapshotStore, IStartupSnapshotStore, IEnvironmentSnapshotStore, IHostsFileSnapshotStore, IFirewallSnapshotStore, IInstalledApplicationSnapshotStore, IFileSystemSnapshotStore
{
    private const int SchemaVersion = 9;

    private static readonly IReadOnlyList<string> SnapshotTables =
    [
        "registry_snapshots",
        "service_snapshots",
        "scheduled_task_snapshots",
        "startup_snapshots",
        "environment_snapshots",
        "hosts_file_snapshots",
        "firewall_snapshots",
        "installed_application_snapshots",
        "file_system_snapshots"
    ];

    private static readonly IReadOnlyList<SqliteMigration> Migrations =
    [
        new(
            SchemaVersion,
            "initial_schema",
            """
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                payload_json TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                status TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS registry_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS service_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS scheduled_task_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS startup_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS environment_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS hosts_file_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS firewall_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS installed_application_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS file_system_snapshots (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                name TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            """)
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath)) ?? ".");

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            """
            PRAGMA journal_mode = WAL;
            """,
            cancellationToken)
            .ConfigureAwait(false);

        await EnsureMigrationTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await ApplyPendingMigrationsAsync(connection, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, $"PRAGMA user_version = {SchemaVersion};", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveSessionAsync(TrackingSession session, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO sessions(id, payload_json, created_at_utc, status)
            VALUES ($id, $payload, $createdAtUtc, $status)
            ON CONFLICT(id) DO UPDATE SET
                payload_json = excluded.payload_json,
                status = excluded.status;
            """,
            cancellationToken,
            new SqliteParameter("$id", session.Id.ToString()),
            new SqliteParameter("$payload", JsonSerializer.Serialize(session, WinLedgerJsonSerializer.Options)),
            new SqliteParameter("$createdAtUtc", session.CreatedAt.UtcDateTime.ToString("O")),
            new SqliteParameter("$status", session.Status.ToString()))
            .ConfigureAwait(false);
    }

    public async Task<TrackingSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM sessions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", sessionId.ToString());

        var payload = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return payload is null
            ? null
            : JsonSerializer.Deserialize<TrackingSession>(payload, WinLedgerJsonSerializer.Options);
    }

    public async Task<IReadOnlyList<TrackingSession>> ListSessionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT payload_json
            FROM sessions
            ORDER BY created_at_utc DESC, id ASC;
            """;

        var sessions = new List<TrackingSession>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var payload = reader.GetString(0);
            var session = JsonSerializer.Deserialize<TrackingSession>(payload, WinLedgerJsonSerializer.Options);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    public async Task<SqliteSessionCleanupResult> CleanupSessionsAsync(
        DateTimeOffset cutoffUtc,
        int keepNewestSessions,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (keepNewestSessions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keepNewestSessions), "Newest session keep count cannot be negative.");
        }

        var normalizedCutoff = cutoffUtc.ToUniversalTime();
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            CREATE TEMP TABLE IF NOT EXISTS session_cleanup_targets (
                id TEXT PRIMARY KEY
            );
            DELETE FROM session_cleanup_targets;
            """,
            cancellationToken)
            .ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            INSERT INTO session_cleanup_targets(id)
            SELECT id
            FROM sessions
            WHERE created_at_utc < $cutoffUtc
              AND (
                  $keepNewestSessions <= 0 OR
                  id NOT IN (
                      SELECT id
                      FROM sessions
                      ORDER BY created_at_utc DESC, id ASC
                      LIMIT $keepNewestSessions
                  )
              );
            """,
            cancellationToken,
            new SqliteParameter("$cutoffUtc", normalizedCutoff.UtcDateTime.ToString("O")),
            new SqliteParameter("$keepNewestSessions", keepNewestSessions))
            .ConfigureAwait(false);

        var matchedSessions = await ExecuteScalarIntAsync(
            connection,
            transaction,
            "SELECT COUNT(*) FROM session_cleanup_targets;",
            cancellationToken)
            .ConfigureAwait(false);
        var matchedSnapshotRows = new Dictionary<string, int>(StringComparer.Ordinal);
        var deletedSnapshotRows = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var table in SnapshotTables)
        {
            var matchedRows = await ExecuteScalarIntAsync(
                connection,
                transaction,
                $"SELECT COUNT(*) FROM {table} WHERE session_id IN (SELECT id FROM session_cleanup_targets);",
                cancellationToken)
                .ConfigureAwait(false);
            matchedSnapshotRows[table] = matchedRows;
            deletedSnapshotRows[table] = 0;
        }

        var deletedSessions = 0;
        if (!dryRun)
        {
            foreach (var table in SnapshotTables)
            {
                deletedSnapshotRows[table] = await ExecuteAffectedRowsAsync(
                    connection,
                    transaction,
                    $"DELETE FROM {table} WHERE session_id IN (SELECT id FROM session_cleanup_targets);",
                    cancellationToken)
                    .ConfigureAwait(false);
            }

            deletedSessions = await ExecuteAffectedRowsAsync(
                connection,
                transaction,
                "DELETE FROM sessions WHERE id IN (SELECT id FROM session_cleanup_targets);",
                cancellationToken)
                .ConfigureAwait(false);

            transaction.Commit();
        }
        else
        {
            transaction.Rollback();
        }

        return new SqliteSessionCleanupResult(
            dryRun,
            normalizedCutoff,
            keepNewestSessions,
            matchedSessions,
            deletedSessions,
            matchedSnapshotRows,
            deletedSnapshotRows);
    }

    public Task SaveRegistrySnapshotAsync(RegistrySnapshot snapshot, CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "registry_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<RegistrySnapshot?> GetRegistrySnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<RegistrySnapshot>("registry_snapshots", snapshotId, cancellationToken);
    }

    public Task<IReadOnlyList<RegistrySnapshot>> ListRegistrySnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<RegistrySnapshot>("registry_snapshots", sessionId, cancellationToken);
    }

    public Task SaveServiceSnapshotAsync(ServiceSnapshot snapshot, CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "service_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<ServiceSnapshot?> GetServiceSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<ServiceSnapshot>("service_snapshots", snapshotId, cancellationToken);
    }

    public Task<IReadOnlyList<ServiceSnapshot>> ListServiceSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<ServiceSnapshot>("service_snapshots", sessionId, cancellationToken);
    }

    public Task SaveScheduledTaskSnapshotAsync(ScheduledTaskSnapshot snapshot, CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "scheduled_task_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<ScheduledTaskSnapshot?> GetScheduledTaskSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<ScheduledTaskSnapshot>("scheduled_task_snapshots", snapshotId, cancellationToken);
    }

    public Task<IReadOnlyList<ScheduledTaskSnapshot>> ListScheduledTaskSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<ScheduledTaskSnapshot>("scheduled_task_snapshots", sessionId, cancellationToken);
    }

    public Task SaveStartupSnapshotAsync(StartupSnapshot snapshot, CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "startup_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<StartupSnapshot?> GetStartupSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<StartupSnapshot>("startup_snapshots", snapshotId, cancellationToken);
    }

    public Task<IReadOnlyList<StartupSnapshot>> ListStartupSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<StartupSnapshot>("startup_snapshots", sessionId, cancellationToken);
    }

    public Task SaveEnvironmentSnapshotAsync(EnvironmentSnapshot snapshot, CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "environment_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<EnvironmentSnapshot?> GetEnvironmentSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<EnvironmentSnapshot>("environment_snapshots", snapshotId, cancellationToken);
    }

    public Task<IReadOnlyList<EnvironmentSnapshot>> ListEnvironmentSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<EnvironmentSnapshot>("environment_snapshots", sessionId, cancellationToken);
    }

    public Task SaveHostsFileSnapshotAsync(HostsFileSnapshot snapshot, CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "hosts_file_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<HostsFileSnapshot?> GetHostsFileSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<HostsFileSnapshot>("hosts_file_snapshots", snapshotId, cancellationToken);
    }

    public Task<IReadOnlyList<HostsFileSnapshot>> ListHostsFileSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<HostsFileSnapshot>("hosts_file_snapshots", sessionId, cancellationToken);
    }

    public Task SaveFirewallSnapshotAsync(FirewallSnapshot snapshot, CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "firewall_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<FirewallSnapshot?> GetFirewallSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<FirewallSnapshot>("firewall_snapshots", snapshotId, cancellationToken);
    }

    public Task<IReadOnlyList<FirewallSnapshot>> ListFirewallSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<FirewallSnapshot>("firewall_snapshots", sessionId, cancellationToken);
    }

    public Task SaveInstalledApplicationsSnapshotAsync(
        InstalledApplicationsSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "installed_application_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<InstalledApplicationsSnapshot?> GetInstalledApplicationsSnapshotAsync(
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<InstalledApplicationsSnapshot>(
            "installed_application_snapshots",
            snapshotId,
            cancellationToken);
    }

    public Task<IReadOnlyList<InstalledApplicationsSnapshot>> ListInstalledApplicationsSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<InstalledApplicationsSnapshot>(
            "installed_application_snapshots",
            sessionId,
            cancellationToken);
    }

    public Task SaveFileSystemSnapshotAsync(FileSystemSnapshot snapshot, CancellationToken cancellationToken)
    {
        return SaveSnapshotAsync(
            "file_system_snapshots",
            snapshot.Id,
            snapshot.SessionId,
            snapshot.Name,
            snapshot.CapturedAt,
            JsonSerializer.Serialize(snapshot, WinLedgerJsonSerializer.Options),
            cancellationToken);
    }

    public Task<FileSystemSnapshot?> GetFileSystemSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        return GetSnapshotAsync<FileSystemSnapshot>("file_system_snapshots", snapshotId, cancellationToken);
    }

    public Task<IReadOnlyList<FileSystemSnapshot>> ListFileSystemSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ListSnapshotsAsync<FileSystemSnapshot>("file_system_snapshots", sessionId, cancellationToken);
    }

    private async Task SaveSnapshotAsync(
        string tableName,
        Guid snapshotId,
        Guid sessionId,
        string name,
        DateTimeOffset capturedAt,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            $"""
            INSERT INTO {tableName}(id, session_id, name, captured_at_utc, payload_json)
            VALUES ($id, $sessionId, $name, $capturedAtUtc, $payload)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                captured_at_utc = excluded.captured_at_utc,
                payload_json = excluded.payload_json;
            """,
            cancellationToken,
            new SqliteParameter("$id", snapshotId.ToString()),
            new SqliteParameter("$sessionId", sessionId.ToString()),
            new SqliteParameter("$name", name),
            new SqliteParameter("$capturedAtUtc", capturedAt.UtcDateTime.ToString("O")),
            new SqliteParameter("$payload", payloadJson))
            .ConfigureAwait(false);
    }

    private async Task<TSnapshot?> GetSnapshotAsync<TSnapshot>(
        string tableName,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT payload_json FROM {tableName} WHERE id = $id;";
        command.Parameters.AddWithValue("$id", snapshotId.ToString());

        var payload = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return payload is null
            ? default
            : JsonSerializer.Deserialize<TSnapshot>(payload, WinLedgerJsonSerializer.Options);
    }

    private async Task<IReadOnlyList<TSnapshot>> ListSnapshotsAsync<TSnapshot>(
        string tableName,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT payload_json
            FROM {tableName}
            WHERE session_id = $sessionId
            ORDER BY captured_at_utc ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());

        var snapshots = new List<TSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var payload = reader.GetString(0);
            var snapshot = JsonSerializer.Deserialize<TSnapshot>(payload, WinLedgerJsonSerializer.Options);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        };

        return new SqliteConnection(builder.ToString());
    }

    private static async Task EnsureMigrationTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                name TEXT NOT NULL DEFAULT '',
                applied_at_utc TEXT NOT NULL
            );
            """,
            cancellationToken)
            .ConfigureAwait(false);

        var columns = await ReadColumnNamesAsync(connection, "schema_migrations", cancellationToken).ConfigureAwait(false);
        if (!columns.Contains("name"))
        {
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE schema_migrations ADD COLUMN name TEXT NOT NULL DEFAULT '';",
                cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task ApplyPendingMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var appliedVersions = await ReadAppliedMigrationVersionsAsync(connection, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();

        foreach (var migration in Migrations)
        {
            if (!appliedVersions.Contains(migration.Version))
            {
                await ExecuteNonQueryAsync(connection, transaction, migration.Sql, cancellationToken).ConfigureAwait(false);
                await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO schema_migrations(version, name, applied_at_utc)
                    VALUES ($version, $name, $appliedAtUtc);
                    """,
                    cancellationToken,
                    new SqliteParameter("$version", migration.Version),
                    new SqliteParameter("$name", migration.Name),
                    new SqliteParameter("$appliedAtUtc", DateTimeOffset.UtcNow.ToString("O")))
                    .ConfigureAwait(false);
            }
            else
            {
                await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    """
                    UPDATE schema_migrations
                    SET name = $name
                    WHERE version = $version AND name = '';
                    """,
                    cancellationToken,
                    new SqliteParameter("$version", migration.Version),
                    new SqliteParameter("$name", migration.Name))
                    .ConfigureAwait(false);
            }
        }

        transaction.Commit();
    }

    private static async Task<HashSet<int>> ReadAppliedMigrationVersionsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_migrations;";

        var versions = new HashSet<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        params SqliteParameter[] parameters)
    {
        await ExecuteNonQueryAsync(connection, null, commandText, cancellationToken, parameters).ConfigureAwait(false);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText,
        CancellationToken cancellationToken,
        params SqliteParameter[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ExecuteAffectedRowsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params SqliteParameter[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ExecuteScalarIntAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params SqliteParameter[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record SqliteMigration(int Version, string Name, string Sql);
}
