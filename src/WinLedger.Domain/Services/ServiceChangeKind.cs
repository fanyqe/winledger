namespace WinLedger.Domain.Services;

public enum ServiceChangeKind
{
    ServiceCreated,
    ServiceRemoved,
    StartModeChanged,
    ExecutablePathChanged,
    DisplayNameChanged,
    ServiceAccountChanged,
    StateChanged,
    DelayedAutoStartChanged,
    DependenciesChanged
}
