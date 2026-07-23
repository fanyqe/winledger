using WinLedger.Domain.Rollback;
using WinLedger.Domain.Startup;

namespace WinLedger.Rollback.Startup;

public sealed class StartupRollbackPlanner
{
    public StartupRollbackPlan CreatePlan(StartupComparison comparison, DateTimeOffset createdAt)
    {
        var operations = new List<StartupRollbackOperation>();
        var warnings = new List<string>();

        foreach (var change in comparison.Changes)
        {
            if (change.Kind == StartupEntryChangeKind.EntryCreated &&
                change.After?.Source == StartupEntrySourceKind.StartupFolder)
            {
                operations.Add(new StartupRollbackOperation(
                    Guid.NewGuid(),
                    change.Id,
                    StartupRollbackOperationKind.DeleteStartupFolderEntry,
                    change.After,
                    RequiresAdministrator(change.After),
                    false));
                continue;
            }

            warnings.Add($"Startup rollback requires native subsystem review: {change.TargetDisplayName}");
        }

        return new StartupRollbackPlan(Guid.NewGuid(), comparison.Id, createdAt, operations, warnings);
    }

    private static bool RequiresAdministrator(StartupEntrySnapshot entry)
    {
        return entry.Location.Contains(@"\ProgramData\", StringComparison.OrdinalIgnoreCase);
    }
}
