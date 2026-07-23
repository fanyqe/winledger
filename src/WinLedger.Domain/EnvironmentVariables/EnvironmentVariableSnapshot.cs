namespace WinLedger.Domain.EnvironmentVariables;

public sealed record EnvironmentVariableSnapshot(
    EnvironmentVariableScopeKind Scope,
    string Name,
    string RawValue,
    EnvironmentVariableValueType ValueType,
    IReadOnlyList<string> PathEntries,
    string SourceKey)
{
    public bool IsPath => string.Equals(Name, "Path", StringComparison.OrdinalIgnoreCase);
}
