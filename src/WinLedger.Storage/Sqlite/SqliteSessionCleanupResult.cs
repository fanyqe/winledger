namespace WinLedger.Storage.Sqlite;

public sealed record SqliteSessionCleanupResult(
    bool DryRun,
    DateTimeOffset CutoffUtc,
    int KeepNewestSessions,
    int MatchedSessions,
    int DeletedSessions,
    IReadOnlyDictionary<string, int> MatchedSnapshotRows,
    IReadOnlyDictionary<string, int> DeletedSnapshotRows)
{
    public int TotalMatchedSnapshotRows => MatchedSnapshotRows.Values.Sum();

    public int TotalDeletedSnapshotRows => DeletedSnapshotRows.Values.Sum();
}
