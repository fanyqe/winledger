namespace WinLedger.Domain.Registry;

public sealed record RegistrySnapshotTarget(
    RegistryPath Path,
    bool IncludeSubKeys = true,
    string? DisplayName = null);
