namespace WinLedger.Core.Elevation;

public sealed record ElevatedRollbackRequest(
    string ProtocolVersion,
    Guid RequestId,
    ElevatedRollbackSubsystem Subsystem,
    string ReportJsonPath,
    string ReportSha256,
    string OperationSelector,
    string AuthenticationTokenSha256,
    string HelperExecutableSha256,
    DateTimeOffset CreatedAtUtc);
