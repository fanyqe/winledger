using WinLedger.Core.Sessions;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Registry;

namespace WinLedger.Cli;

internal sealed record SessionCaptureOptions(
    IReadOnlyList<TrackingSubsystemKind> Subsystems,
    IReadOnlyList<RegistrySnapshotTarget>? RegistryTargets,
    FileSystemSnapshotOptions? FileSystemOptions);
