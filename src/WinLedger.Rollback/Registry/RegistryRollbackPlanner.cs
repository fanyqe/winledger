using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.Registry;

public sealed class RegistryRollbackPlanner
{
    public RegistryRollbackPlan CreatePlan(RegistryComparison comparison, DateTimeOffset createdAt)
    {
        var operations = new List<RegistryRollbackOperation>();
        var warnings = new List<string>();

        foreach (var change in comparison.Changes)
        {
            if (change.ValueName is null)
            {
                warnings.Add($"Registry key rollback requires manual review: {change.TargetDisplayName}");
                continue;
            }

            var requiresAdministrator = change.KeyPath.Hive is RegistryHiveKind.LocalMachine
                or RegistryHiveKind.ClassesRoot
                or RegistryHiveKind.CurrentConfig;

            switch (change.Kind)
            {
                case RegistryChangeKind.ValueCreated:
                    operations.Add(new RegistryRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        RollbackOperationKind.DeleteRegistryValue,
                        change.KeyPath,
                        change.ValueName,
                        change.After,
                        null,
                        requiresAdministrator,
                        RequiresRestart(change)));
                    break;

                case RegistryChangeKind.ValueRemoved:
                case RegistryChangeKind.ValueModified:
                case RegistryChangeKind.ValueTypeChanged:
                    operations.Add(new RegistryRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        RollbackOperationKind.SetRegistryValue,
                        change.KeyPath,
                        change.ValueName,
                        change.After,
                        change.Before,
                        requiresAdministrator,
                        RequiresRestart(change)));
                    break;

                default:
                    warnings.Add($"Unsupported registry rollback change kind: {change.Kind} at {change.TargetDisplayName}");
                    break;
            }
        }

        return new RegistryRollbackPlan(Guid.NewGuid(), comparison.Id, createdAt, operations, warnings);
    }

    private static bool RequiresRestart(RegistryChange change)
    {
        var fullPath = change.KeyPath.FullPath.ToUpperInvariant();
        return fullPath.Contains("\\SERVICES\\", StringComparison.Ordinal) ||
               fullPath.Contains("\\SESSION MANAGER\\ENVIRONMENT", StringComparison.Ordinal);
    }
}
