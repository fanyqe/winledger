namespace WinLedger.Domain.Rollback;

public enum FileSystemRollbackOperationKind
{
    DeleteCreatedEntry,
    RestoreDeletedFile,
    RestoreModifiedFile
}
