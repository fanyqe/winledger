using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.EnvironmentVariables;

public sealed class EnvironmentRollbackPlanner
{
    public EnvironmentRollbackPlan CreatePlan(EnvironmentComparison comparison, DateTimeOffset createdAt)
    {
        var operations = new List<EnvironmentRollbackOperation>();
        var warnings = new List<string>();
        var plannedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in comparison.Changes)
        {
            var identity = $"{change.Scope}|{change.Name}";
            if (!plannedVariables.Add(identity))
            {
                continue;
            }

            switch (change.Kind)
            {
                case EnvironmentVariableChangeKind.VariableCreated:
                    if (change.After is null)
                    {
                        warnings.Add($"Environment rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new EnvironmentRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        EnvironmentRollbackOperationKind.DeleteEnvironmentVariable,
                        change.Scope,
                        change.Name,
                        change.After,
                        null,
                        RequiresAdministrator(change.Scope),
                        true));
                    break;

                case EnvironmentVariableChangeKind.VariableRemoved:
                    if (change.Before is null)
                    {
                        warnings.Add($"Environment rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    if (!CanSet(change.Before))
                    {
                        warnings.Add($"Environment rollback cannot restore unsupported value type: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new EnvironmentRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        EnvironmentRollbackOperationKind.SetEnvironmentVariable,
                        change.Scope,
                        change.Name,
                        null,
                        change.Before,
                        RequiresAdministrator(change.Scope),
                        true));
                    break;

                case EnvironmentVariableChangeKind.ValueChanged:
                case EnvironmentVariableChangeKind.PathEntryAdded:
                case EnvironmentVariableChangeKind.PathEntryRemoved:
                case EnvironmentVariableChangeKind.PathEntryReordered:
                    if (change.Before is null || change.After is null)
                    {
                        warnings.Add($"Environment rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    if (!CanSet(change.Before))
                    {
                        warnings.Add($"Environment rollback cannot restore unsupported value type: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new EnvironmentRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        EnvironmentRollbackOperationKind.SetEnvironmentVariable,
                        change.Scope,
                        change.Name,
                        change.After,
                        change.Before,
                        RequiresAdministrator(change.Scope),
                        true));
                    break;

                default:
                    warnings.Add($"Unsupported environment rollback change kind: {change.Kind} at {change.TargetDisplayName}");
                    break;
            }
        }

        return new EnvironmentRollbackPlan(Guid.NewGuid(), comparison.Id, createdAt, operations, warnings);
    }

    private static bool RequiresAdministrator(EnvironmentVariableScopeKind scope)
    {
        return scope == EnvironmentVariableScopeKind.Machine;
    }

    private static bool CanSet(EnvironmentVariableSnapshot variable)
    {
        return variable.ValueType is EnvironmentVariableValueType.String or EnvironmentVariableValueType.ExpandString;
    }
}
