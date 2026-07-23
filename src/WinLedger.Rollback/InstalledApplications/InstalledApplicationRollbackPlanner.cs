using WinLedger.Domain.InstalledApplications;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.InstalledApplications;

public sealed class InstalledApplicationRollbackPlanner
{
    public InstalledApplicationRollbackPlan CreatePlan(
        InstalledApplicationsComparison comparison,
        DateTimeOffset createdAt)
    {
        var warnings = comparison.Changes
            .Select(change => $"Installed application rollback requires manual review for {change.Kind}: {change.TargetDisplayName}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new InstalledApplicationRollbackPlan(
            Guid.NewGuid(),
            comparison.Id,
            createdAt,
            Array.Empty<InstalledApplicationRollbackOperation>(),
            warnings);
    }
}
