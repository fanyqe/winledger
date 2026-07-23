namespace WinLedger.Domain.EnvironmentVariables;

public enum EnvironmentVariableChangeKind
{
    VariableCreated,
    VariableRemoved,
    ValueChanged,
    PathEntryAdded,
    PathEntryRemoved,
    PathEntryReordered
}
