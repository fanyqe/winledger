namespace WinLedger.Domain.Startup;

public enum StartupEntrySourceKind
{
    RegistryRun,
    RegistryRunOnce,
    StartupFolder,
    ScheduledTask,
    WindowsService
}
