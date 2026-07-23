using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.FileSystem;

public sealed record FileSystemChange(
    Guid Id,
    FileSystemChangeKind Kind,
    FileSystemEntryKind EntryKind,
    string Path,
    string? PreviousPath,
    FileSystemEntrySnapshot? Before,
    FileSystemEntrySnapshot? After,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => Path;
}
