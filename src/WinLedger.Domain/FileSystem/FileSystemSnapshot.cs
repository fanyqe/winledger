namespace WinLedger.Domain.FileSystem;

public sealed record FileSystemSnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    FileSystemSnapshotOptions Options,
    IReadOnlyList<FileSystemEntrySnapshot> Entries,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<FileSystemChangeJournalState> ChangeJournalStates { get; init; } =
        Array.Empty<FileSystemChangeJournalState>();
}
