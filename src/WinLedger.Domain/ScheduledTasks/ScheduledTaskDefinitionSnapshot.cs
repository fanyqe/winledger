namespace WinLedger.Domain.ScheduledTasks;

public sealed record ScheduledTaskDefinitionSnapshot(
    string FullPath,
    string FolderPath,
    string Name,
    bool Enabled,
    ScheduledTaskStateKind State,
    string? RunAsUser,
    ScheduledTaskPrivilegeLevelKind PrivilegeLevel,
    IReadOnlyList<ScheduledTaskActionSnapshot> Actions,
    IReadOnlyList<ScheduledTaskTriggerSnapshot> Triggers,
    string DefinitionXml)
{
    public string DisplayName => FullPath;
}
