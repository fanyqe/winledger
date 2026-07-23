using WinLedger.Domain.FileSystem;

namespace WinLedger.Domain.Rollback;

public sealed record FileSystemRollbackOperation(
    Guid Id,
    Guid ChangeId,
    FileSystemRollbackOperationKind Kind,
    string RootPath,
    string TargetPath,
    FileSystemEntryKind EntryKind,
    FileSystemEntrySnapshot? ExpectedCurrentEntry,
    FileSystemEntrySnapshot? RestoreEntry,
    string? RestoreContentBase64,
    bool RequiresAdministrator,
    bool RequiresRestart)
{
    public string TargetDisplayName => TargetPath;
}
