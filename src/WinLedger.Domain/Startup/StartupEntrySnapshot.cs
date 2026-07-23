namespace WinLedger.Domain.Startup;

public sealed record StartupEntrySnapshot(
    string StableId,
    StartupEntrySourceKind Source,
    string Name,
    string Location,
    string? Command,
    bool Enabled,
    string? RunAsUser,
    string? TriggerDescription,
    string SourceSubsystem,
    long? FileSize,
    DateTimeOffset? LastWriteTimeUtc);
