using WinLedger.Domain.Registry;

namespace WinLedger.Collectors.Registry;

public sealed record RegistryTrackingProfile(
    string Name,
    string DisplayName,
    string Description,
    IReadOnlyList<RegistrySnapshotTarget> Targets);

public static class DefaultRegistrySnapshotTargets
{
    public const string DefaultProfileName = "installer";

    private static readonly IReadOnlyList<RegistrySnapshotTarget> UserTargets =
    [
        Target(RegistryHiveKind.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "User startup entries"),
        Target(RegistryHiveKind.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "One-time user startup entries"),
        Target(RegistryHiveKind.CurrentUser, "Environment", "User environment variables"),
        Target(RegistryHiveKind.CurrentUser, @"Software\Policies", "User policies"),
        Target(RegistryHiveKind.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "User installed applications"),
        Target(RegistryHiveKind.CurrentUser, @"Software\Classes\*\shell", "User file shell integration"),
        Target(RegistryHiveKind.CurrentUser, @"Software\Classes\Directory\shell", "User directory shell integration")
    ];

    private static readonly IReadOnlyList<RegistrySnapshotTarget> MachineTargets =
    [
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Machine startup entries", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Machine startup entries", RegistryViewKind.Registry32),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "One-time machine startup entries", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "One-time machine startup entries", RegistryViewKind.Registry32),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Policies", "Machine policies", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Policies", "Machine policies", RegistryViewKind.Registry32),
        Target(RegistryHiveKind.LocalMachine, @"SYSTEM\CurrentControlSet\Services", "Windows service configuration"),
        Target(RegistryHiveKind.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", "Machine environment variables"),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "Machine installed applications", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "Machine installed applications", RegistryViewKind.Registry32),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Classes\*\shell", "Machine file shell integration", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Classes\*\shell", "Machine file shell integration", RegistryViewKind.Registry32),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Classes\Directory\shell", "Machine directory shell integration", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Classes\Directory\shell", "Machine directory shell integration", RegistryViewKind.Registry32)
    ];

    private static readonly IReadOnlyList<RegistrySnapshotTarget> StartupTargets =
    [
        Target(RegistryHiveKind.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "User startup entries"),
        Target(RegistryHiveKind.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "One-time user startup entries"),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Machine startup entries", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Machine startup entries", RegistryViewKind.Registry32),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "One-time machine startup entries", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "One-time machine startup entries", RegistryViewKind.Registry32),
        Target(RegistryHiveKind.LocalMachine, @"SYSTEM\CurrentControlSet\Services", "Windows service configuration")
    ];

    private static readonly IReadOnlyList<RegistrySnapshotTarget> PolicyTargets =
    [
        Target(RegistryHiveKind.CurrentUser, @"Software\Policies", "User policies"),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Policies", "Machine policies", RegistryViewKind.Registry64),
        Target(RegistryHiveKind.LocalMachine, @"SOFTWARE\Policies", "Machine policies", RegistryViewKind.Registry32)
    ];

    public static RegistryTrackingProfile InstallerProfile { get; } = new(
        DefaultProfileName,
        "Installer activity",
        "Startup, services, policies, environment, shell integration, and installed application registration roots.",
        NormalizeTargets(UserTargets.Concat(MachineTargets)));

    public static RegistryTrackingProfile UserProfile { get; } = new(
        "user",
        "Current user",
        "Current-user startup, environment, policy, shell integration, and application registration roots.",
        UserTargets);

    public static RegistryTrackingProfile MachineProfile { get; } = new(
        "machine",
        "Machine-wide",
        "Machine-wide startup, service, policy, environment, shell integration, and application registration roots.",
        MachineTargets);

    public static RegistryTrackingProfile StartupProfile { get; } = new(
        "startup",
        "Startup persistence",
        "Registry roots commonly used for Windows startup and service persistence.",
        StartupTargets);

    public static RegistryTrackingProfile PolicyProfile { get; } = new(
        "policy",
        "Policy changes",
        "User and machine policy registry roots.",
        PolicyTargets);

    public static RegistryTrackingProfile SandboxProfile { get; } = new(
        "sandbox",
        "Test sandbox",
        "WinLedger test sandbox under the current user hive.",
        [Target(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox", "WinLedger test sandbox")]);

    public static RegistryTrackingProfile DefaultProfile => InstallerProfile;

    public static IReadOnlyList<RegistryTrackingProfile> Profiles { get; } =
    [
        InstallerProfile,
        UserProfile,
        MachineProfile,
        StartupProfile,
        PolicyProfile,
        SandboxProfile
    ];

    public static IReadOnlyList<RegistrySnapshotTarget> ConservativeUserTargets => UserProfile.Targets;

    public static IReadOnlyList<RegistrySnapshotTarget> MinimalSandboxTargets => SandboxProfile.Targets;

    public static RegistryTrackingProfile ResolveProfile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultProfile;
        }

        return Profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Unknown registry profile '{name}'. Available profiles: {string.Join(", ", Profiles.Select(profile => profile.Name))}.");
    }

    public static RegistryTrackingProfile? FindProfileForTargets(IReadOnlyList<RegistrySnapshotTarget> targets)
    {
        return Profiles.FirstOrDefault(profile => TargetsEqual(profile.Targets, targets));
    }

    public static IReadOnlyList<RegistrySnapshotTarget> NormalizeTargets(IEnumerable<RegistrySnapshotTarget> targets)
    {
        var normalized = new List<RegistrySnapshotTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in targets)
        {
            if (seen.Add(TargetKey(target)))
            {
                normalized.Add(target);
            }
        }

        return normalized;
    }

    private static RegistrySnapshotTarget Target(
        RegistryHiveKind hive,
        string keyPath,
        string displayName,
        RegistryViewKind view = RegistryViewKind.Default)
    {
        return new RegistrySnapshotTarget(new RegistryPath(hive, keyPath, view), IncludeSubKeys: true, displayName);
    }

    private static bool TargetsEqual(
        IReadOnlyList<RegistrySnapshotTarget> left,
        IReadOnlyList<RegistrySnapshotTarget> right)
    {
        return left.Count == right.Count &&
               NormalizeTargets(left).Select(TargetKey).SequenceEqual(NormalizeTargets(right).Select(TargetKey), StringComparer.OrdinalIgnoreCase);
    }

    private static string TargetKey(RegistrySnapshotTarget target)
    {
        return $"{target.Path.FullPath}|{target.Path.View}|{target.IncludeSubKeys}";
    }
}
