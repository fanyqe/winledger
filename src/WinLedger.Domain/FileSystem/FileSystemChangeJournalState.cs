namespace WinLedger.Domain.FileSystem;

public sealed record FileSystemChangeJournalState(
    string VolumeRootPath,
    string FileSystemName,
    bool IsAvailable,
    ulong? JournalId,
    long? FirstUsn,
    long? NextUsn,
    long? LowestValidUsn,
    long? MaxUsn,
    string? UnavailableReason);
