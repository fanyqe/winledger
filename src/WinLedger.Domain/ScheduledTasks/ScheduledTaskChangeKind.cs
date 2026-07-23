namespace WinLedger.Domain.ScheduledTasks;

public enum ScheduledTaskChangeKind
{
    TaskCreated,
    TaskRemoved,
    EnabledChanged,
    ActionChanged,
    TriggerChanged,
    RunAsUserChanged,
    PrivilegeLevelChanged,
    DefinitionChanged
}
