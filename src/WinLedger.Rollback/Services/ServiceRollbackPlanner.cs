using WinLedger.Domain.Rollback;
using WinLedger.Domain.Services;

namespace WinLedger.Rollback.Services;

public sealed class ServiceRollbackPlanner
{
    public ServiceRollbackPlan CreatePlan(ServiceComparison comparison, DateTimeOffset createdAt)
    {
        var operations = new List<ServiceRollbackOperation>();
        var warnings = new List<string>();

        foreach (var change in comparison.Changes)
        {
            if (change.Before is null || change.After is null)
            {
                warnings.Add($"Service rollback requires manual review: {change.TargetDisplayName}");
                continue;
            }

            switch (change.Kind)
            {
                case ServiceChangeKind.StartModeChanged:
                    if (change.Before.StartMode is ServiceStartModeKind.Unknown ||
                        change.After.StartMode is ServiceStartModeKind.Unknown)
                    {
                        warnings.Add($"Service start mode rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new ServiceRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        ServiceRollbackOperationKind.SetServiceStartMode,
                        change.ServiceName,
                        change.After,
                        change.Before.StartMode,
                        null,
                        true,
                        true));
                    break;

                case ServiceChangeKind.DelayedAutoStartChanged:
                    if (change.Before.DelayedAutoStart is null || change.After.DelayedAutoStart is null)
                    {
                        warnings.Add($"Delayed auto-start rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new ServiceRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        ServiceRollbackOperationKind.SetServiceDelayedAutoStart,
                        change.ServiceName,
                        change.After,
                        null,
                        change.Before.DelayedAutoStart.Value,
                        true,
                        true));
                    break;

                default:
                    warnings.Add($"Unsupported service rollback change kind: {change.Kind} at {change.TargetDisplayName}");
                    break;
            }
        }

        return new ServiceRollbackPlan(Guid.NewGuid(), comparison.Id, createdAt, operations, warnings);
    }
}
