using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.Startup;

public sealed record StartupChange(
    Guid Id,
    StartupEntryChangeKind Kind,
    string StableId,
    StartupEntrySnapshot? Before,
    StartupEntrySnapshot? After,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => After?.Name ?? Before?.Name ?? StableId;
}
