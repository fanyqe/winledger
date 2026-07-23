namespace WinLedger.Core.Elevation;

public sealed record ElevatedRollbackResponse(
    Guid RequestId,
    bool Authenticated,
    bool Succeeded,
    IReadOnlyList<ElevatedRollbackOperationResult> Results,
    IReadOnlyList<string> Warnings);
