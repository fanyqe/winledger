namespace WinLedger.Core.Elevation;

public enum ElevatedRollbackSubsystem
{
    Registry,
    Services,
    ScheduledTasks,
    Startup,
    Environment,
    HostsFile,
    Firewall,
    FileSystem
}
