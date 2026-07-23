namespace WinLedger.Core.Sessions;

public sealed record TrackingSubsystemSelection(
    bool IncludeRegistry,
    bool IncludeServices,
    bool IncludeScheduledTasks,
    bool IncludeStartup,
    bool IncludeEnvironmentVariables,
    bool IncludeHostsFile,
    bool IncludeFirewall,
    bool IncludeInstalledApplications,
    bool IncludeFileSystem)
{
    public IReadOnlyList<TrackingSubsystemKind> ToSubsystems()
    {
        var subsystems = new List<TrackingSubsystemKind>();
        if (IncludeRegistry)
        {
            subsystems.Add(TrackingSubsystemKind.Registry);
        }

        if (IncludeServices)
        {
            subsystems.Add(TrackingSubsystemKind.Services);
        }

        if (IncludeScheduledTasks)
        {
            subsystems.Add(TrackingSubsystemKind.ScheduledTasks);
        }

        if (IncludeStartup)
        {
            subsystems.Add(TrackingSubsystemKind.Startup);
        }

        if (IncludeEnvironmentVariables)
        {
            subsystems.Add(TrackingSubsystemKind.EnvironmentVariables);
        }

        if (IncludeHostsFile)
        {
            subsystems.Add(TrackingSubsystemKind.HostsFile);
        }

        if (IncludeFirewall)
        {
            subsystems.Add(TrackingSubsystemKind.Firewall);
        }

        if (IncludeInstalledApplications)
        {
            subsystems.Add(TrackingSubsystemKind.InstalledApplications);
        }

        if (IncludeFileSystem)
        {
            subsystems.Add(TrackingSubsystemKind.FileSystem);
        }

        if (subsystems.Count == 0)
        {
            throw new InvalidOperationException("Select at least one tracking area.");
        }

        return subsystems;
    }
}
