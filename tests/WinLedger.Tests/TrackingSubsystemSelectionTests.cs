using WinLedger.Core.Sessions;

namespace WinLedger.Tests;

public sealed class TrackingSubsystemSelectionTests
{
    [Fact]
    public void ToSubsystemsKeepsUnifiedCaptureOrder()
    {
        var subsystems = new TrackingSubsystemSelection(
            IncludeRegistry: true,
            IncludeServices: true,
            IncludeScheduledTasks: false,
            IncludeStartup: true,
            IncludeEnvironmentVariables: false,
            IncludeHostsFile: true,
            IncludeFirewall: false,
            IncludeInstalledApplications: true,
            IncludeFileSystem: true).ToSubsystems();

        Assert.Equal(
            [
                TrackingSubsystemKind.Registry,
                TrackingSubsystemKind.Services,
                TrackingSubsystemKind.Startup,
                TrackingSubsystemKind.HostsFile,
                TrackingSubsystemKind.InstalledApplications,
                TrackingSubsystemKind.FileSystem
            ],
            subsystems);
    }

    [Fact]
    public void ToSubsystemsRejectsEmptySelection()
    {
        var selection = new TrackingSubsystemSelection(false, false, false, false, false, false, false, false, false);

        Assert.Throws<InvalidOperationException>(() => selection.ToSubsystems());
    }
}
