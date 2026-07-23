namespace WinLedger.Domain.FileSystem;

public sealed record FileSystemEntrySnapshot(
    string Identity,
    string Path,
    string RootPath,
    string RelativePath,
    FileSystemEntryKind Kind,
    long? SizeBytes,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? LastWriteTimeUtc,
    string Attributes,
    string? Sha256,
    bool HasRollbackData,
    string? BackupContentBase64,
    bool IsTemporaryOrHighNoise);
