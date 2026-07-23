namespace WinLedger.Core.Sessions;

public enum TrackingSubsystemKind
{
    Registry,
    Services,
    ScheduledTasks,
    Startup,
    EnvironmentVariables,
    HostsFile,
    Firewall,
    InstalledApplications,
    FileSystem
}
