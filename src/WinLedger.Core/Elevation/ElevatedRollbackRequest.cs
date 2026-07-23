namespace WinLedger.Core.Elevation;

public sealed record ElevatedRollbackRequest(
    string ProtocolVersion,
    Guid RequestId,
    ElevatedRollbackSubsystem Subsystem,
    string ReportJsonPath,
    string OperationSelector,
    string AuthenticationTokenSha256,
    DateTimeOffset CreatedAtUtc);
