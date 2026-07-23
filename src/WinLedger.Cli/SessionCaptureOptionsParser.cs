using WinLedger.Collectors.Registry;
using WinLedger.Core.Sessions;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Registry;

namespace WinLedger.Cli;

internal static class SessionCaptureOptionsParser
{
    private static readonly IReadOnlyList<TrackingSubsystemKind> DefaultCaptureSubsystems =
    [
        TrackingSubsystemKind.Services,
        TrackingSubsystemKind.ScheduledTasks,
        TrackingSubsystemKind.Startup,
        TrackingSubsystemKind.EnvironmentVariables,
        TrackingSubsystemKind.HostsFile,
        TrackingSubsystemKind.Firewall,
        TrackingSubsystemKind.InstalledApplications
    ];

    private static readonly IReadOnlyList<TrackingSubsystemKind> AllCaptureSubsystems =
    [
        TrackingSubsystemKind.Registry,
        TrackingSubsystemKind.Services,
        TrackingSubsystemKind.ScheduledTasks,
        TrackingSubsystemKind.Startup,
        TrackingSubsystemKind.EnvironmentVariables,
        TrackingSubsystemKind.HostsFile,
        TrackingSubsystemKind.Firewall,
        TrackingSubsystemKind.InstalledApplications,
        TrackingSubsystemKind.FileSystem
    ];

    public static SessionCaptureOptions Parse(IReadOnlyList<string> options)
    {
        var requestedSubsystems = new List<TrackingSubsystemKind>();
        var registryTargets = new List<RegistrySnapshotTarget>();
        var fileRoots = new List<string>();
        var calculateHashes = false;
        var backupSmallFiles = false;
        var backupSizeLimitBytes = 0L;
        var includeHighNoise = false;
        var fileOptionUsed = false;

        for (var index = 0; index < options.Count; index++)
        {
            switch (options[index])
            {
                case "--subsystems":
                    EnsureOptionValue(options, index);
                    requestedSubsystems.AddRange(ParseSubsystems(options[++index]));
                    break;

                case "--registry-sandbox":
                    registryTargets.AddRange(DefaultRegistrySnapshotTargets.MinimalSandboxTargets);
                    break;

                case "--registry-profile":
                    EnsureOptionValue(options, index);
                    registryTargets.AddRange(DefaultRegistrySnapshotTargets.ResolveProfile(options[++index]).Targets);
                    break;

                case "--registry-path":
                    EnsureOptionValue(options, index);
                    registryTargets.Add(new RegistrySnapshotTarget(RegistryPath.Parse(options[++index]), IncludeSubKeys: true));
                    break;

                case "--files-root":
                    EnsureOptionValue(options, index);
                    fileRoots.Add(options[++index]);
                    fileOptionUsed = true;
                    break;

                case "--hash":
                    calculateHashes = true;
                    fileOptionUsed = true;
                    break;

                case "--include-noise":
                    includeHighNoise = true;
                    fileOptionUsed = true;
                    break;

                case "--backup-small-files":
                    EnsureOptionValue(options, index);
                    backupSmallFiles = true;
                    backupSizeLimitBytes = long.Parse(options[++index], System.Globalization.CultureInfo.InvariantCulture);
                    fileOptionUsed = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown session capture option: {options[index]}");
            }
        }

        var subsystems = requestedSubsystems.Count > 0
            ? requestedSubsystems
            : DefaultCaptureSubsystems.ToList();

        if (requestedSubsystems.Count == 0 && registryTargets.Count > 0)
        {
            subsystems.Insert(0, TrackingSubsystemKind.Registry);
        }

        if (subsystems.Contains(TrackingSubsystemKind.Registry) && registryTargets.Count == 0)
        {
            registryTargets.AddRange(DefaultRegistrySnapshotTargets.DefaultProfile.Targets);
        }

        if (requestedSubsystems.Count == 0 && fileRoots.Count > 0)
        {
            subsystems.Add(TrackingSubsystemKind.FileSystem);
        }

        if (fileOptionUsed && fileRoots.Count == 0)
        {
            throw new ArgumentException("File-system capture options require at least one --files-root value.");
        }

        var normalizedSubsystems = subsystems.Distinct().ToArray();
        var fileSystemOptions = fileRoots.Count > 0
            ? new FileSystemSnapshotOptions(
                fileRoots,
                FileSystemSnapshotOptions.DefaultExclusionPatterns,
                includeHighNoise,
                calculateHashes,
                backupSmallFiles,
                backupSizeLimitBytes)
            : null;

        return new SessionCaptureOptions(
            normalizedSubsystems,
            registryTargets.Count > 0 ? DefaultRegistrySnapshotTargets.NormalizeTargets(registryTargets) : null,
            fileSystemOptions);
    }

    private static IEnumerable<TrackingSubsystemKind> ParseSubsystems(string value)
    {
        foreach (var subsystemName in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (subsystemName.ToLowerInvariant())
            {
                case "all":
                    foreach (var subsystem in AllCaptureSubsystems)
                    {
                        yield return subsystem;
                    }

                    break;

                case "registry":
                case "reg":
                    yield return TrackingSubsystemKind.Registry;
                    break;

                case "services":
                case "service":
                    yield return TrackingSubsystemKind.Services;
                    break;

                case "tasks":
                case "scheduled-tasks":
                case "scheduledtasks":
                    yield return TrackingSubsystemKind.ScheduledTasks;
                    break;

                case "startup":
                case "startup-entries":
                    yield return TrackingSubsystemKind.Startup;
                    break;

                case "environment":
                case "environment-variables":
                case "env":
                    yield return TrackingSubsystemKind.EnvironmentVariables;
                    break;

                case "hosts":
                case "hosts-file":
                    yield return TrackingSubsystemKind.HostsFile;
                    break;

                case "firewall":
                    yield return TrackingSubsystemKind.Firewall;
                    break;

                case "applications":
                case "apps":
                case "installed-applications":
                    yield return TrackingSubsystemKind.InstalledApplications;
                    break;

                case "files":
                case "file-system":
                case "filesystem":
                    yield return TrackingSubsystemKind.FileSystem;
                    break;

                default:
                    throw new ArgumentException($"Unknown tracking subsystem: {subsystemName}");
            }
        }
    }

    private static void EnsureOptionValue(IReadOnlyList<string> options, int index)
    {
        if (index + 1 >= options.Count)
        {
            throw new ArgumentException($"{options[index]} requires a value.");
        }
    }
}
