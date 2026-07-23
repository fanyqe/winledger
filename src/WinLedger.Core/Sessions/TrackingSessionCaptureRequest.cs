using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Registry;

namespace WinLedger.Core.Sessions;

public sealed record TrackingSessionCaptureRequest(
    Guid SessionId,
    string SnapshotName,
    TrackingSnapshotStage Stage,
    IReadOnlyList<TrackingSubsystemKind> Subsystems,
    IReadOnlyList<RegistrySnapshotTarget>? RegistryTargets,
    FileSystemSnapshotOptions? FileSystemOptions);
