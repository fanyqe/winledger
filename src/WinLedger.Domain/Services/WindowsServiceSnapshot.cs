namespace WinLedger.Domain.Services;

public sealed record WindowsServiceSnapshot(
    string Name,
    string DisplayName,
    ServiceStartModeKind StartMode,
    string? ExecutablePath,
    string? ServiceAccount,
    ServiceStateKind State,
    bool? DelayedAutoStart,
    IReadOnlyList<string> Dependencies,
    string? Description);
