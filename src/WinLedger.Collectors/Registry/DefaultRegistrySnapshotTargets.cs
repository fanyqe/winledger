using WinLedger.Domain.Registry;

namespace WinLedger.Collectors.Registry;

public static class DefaultRegistrySnapshotTargets
{
    public static IReadOnlyList<RegistrySnapshotTarget> ConservativeUserTargets { get; } =
    [
        new(new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"), true, "User startup entries"),
        new(new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce"), true, "One-time user startup entries"),
        new(new RegistryPath(RegistryHiveKind.CurrentUser, "Environment"), true, "User environment variables"),
        new(new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\Policies"), true, "User policies")
    ];

    public static IReadOnlyList<RegistrySnapshotTarget> MinimalSandboxTargets { get; } =
    [
        new(new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox"), true, "WinLedger test sandbox")
    ];
}
