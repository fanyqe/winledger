using WinLedger.Domain.Hosts;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.Hosts;

public sealed class HostsFileRollbackPlanner
{
    public HostsFileRollbackPlan CreatePlan(HostsFileComparison comparison, DateTimeOffset createdAt)
    {
        var operations = new List<HostsFileRollbackOperation>();
        var warnings = new List<string>();
        var plannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in comparison.Changes)
        {
            if (!plannedPaths.Add(change.FilePath))
            {
                continue;
            }

            if (change.Before is { Exists: true, ContentBase64: not null } && change.After is not null)
            {
                operations.Add(new HostsFileRollbackOperation(
                    Guid.NewGuid(),
                    change.Id,
                    HostsFileRollbackOperationKind.RestoreHostsFileContent,
                    change.FilePath,
                    change.After,
                    change.Before.ContentBase64,
                    true,
                    false));
                continue;
            }

            if (change.Before is { Exists: false } && change.After is { Exists: true })
            {
                operations.Add(new HostsFileRollbackOperation(
                    Guid.NewGuid(),
                    change.Id,
                    HostsFileRollbackOperationKind.DeleteHostsFile,
                    change.FilePath,
                    change.After,
                    null,
                    true,
                    false));
                continue;
            }

            warnings.Add($"Hosts file rollback requires manual review: {change.TargetDisplayName}");
        }

        return new HostsFileRollbackPlan(Guid.NewGuid(), comparison.Id, createdAt, operations, warnings);
    }
}
