using WinLedger.Comparison.InstalledApplications;
using WinLedger.Domain.InstalledApplications;
using WinLedger.Domain.Rollback;

namespace WinLedger.Tests;

public sealed class InstalledApplicationsSnapshotComparerTests
{
    [Fact]
    public void CompareDetectsRegisteredRemovedAndVersionChanges()
    {
        var sessionId = Guid.NewGuid();
        var removed = InstalledApplicationTestData.Application("Removed App", "RemovedApp");
        var before = InstalledApplicationTestData.Application("Existing App", "ExistingApp", displayVersion: "1.0.0");
        var after = before with { DisplayVersion = "2.0.0" };
        var registered = InstalledApplicationTestData.Application("Created App", "CreatedApp");

        var result = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId, removed, before),
            InstalledApplicationTestData.Snapshot(sessionId, after, registered),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change =>
            change.Kind == InstalledApplicationChangeKind.ApplicationRegistered &&
            change.TargetDisplayName == "Created App" &&
            change.RollbackAvailability == RollbackAvailability.ManualReview &&
            change.Labels.Contains(ChangeAttentionLabel.Persistent) &&
            change.Labels.Contains(ChangeAttentionLabel.Privileged));
        Assert.Contains(result.Changes, change =>
            change.Kind == InstalledApplicationChangeKind.ApplicationRemoved &&
            change.TargetDisplayName == "Removed App" &&
            change.Labels.Contains(ChangeAttentionLabel.PotentiallyDestructive));
        Assert.Contains(result.Changes, change =>
            change.Kind == InstalledApplicationChangeKind.VersionChanged &&
            change.TargetDisplayName == "Existing App");
    }

    [Fact]
    public void CompareDetectsInstalledApplicationPropertyChanges()
    {
        var sessionId = Guid.NewGuid();
        var before = InstalledApplicationTestData.Application("Changed App", "ChangedApp");
        var after = before with
        {
            DisplayName = "Changed App Pro",
            Publisher = "New Publisher",
            InstallLocation = @"D:\Apps\Changed",
            InstallSource = @"D:\Installers",
            InstallDate = "20260724",
            UninstallString = @"""D:\Apps\Changed\remove.exe""",
            QuietUninstallString = @"""D:\Apps\Changed\remove.exe"" /silent",
            ModifyPath = @"""D:\Apps\Changed\setup.exe"" /modify",
            EstimatedSizeKb = 2048,
            WindowsInstaller = true,
            SystemComponent = true,
            ReleaseType = "Update",
            Comments = "Updated comments",
            UrlInfoAbout = "https://example.test/changed",
            PackageFullName = "Changed.App_1.0.0.0_x64__publisherid",
            PackageFamilyName = "Changed.App_publisherid",
            PackageName = "Changed.App",
            PackagePublisherId = "publisherid",
            PackageResourceId = "neutral",
            PackageManifestPath = @"C:\Program Files\WindowsApps\Changed.App_1.0.0.0_x64__publisherid\AppxManifest.xml",
            AppxInboxPackage = true
        };

        var result = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId, before),
            InstalledApplicationTestData.Snapshot(sessionId, after),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.DisplayNameChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PublisherChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.InstallLocationChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.InstallSourceChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.InstallDateChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.UninstallCommandChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.QuietUninstallCommandChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.ModifyPathChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.EstimatedSizeChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.WindowsInstallerChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.SystemComponentChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.ReleaseTypeChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.CommentsChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.UrlInfoChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PackageFullNameChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PackageFamilyNameChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PackageNameChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PackagePublisherIdChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PackageResourceIdChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PackageManifestPathChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.AppxInboxPackageChanged);
    }

    [Fact]
    public void CompareDetectsAppxPackageVersionAndManifestChanges()
    {
        var sessionId = Guid.NewGuid();
        var before = InstalledApplicationTestData.AppxPackage("Example Package");
        var after = before with
        {
            KeyName = "Example.Package_2.0.0.0_x64__publisherid",
            DisplayVersion = "2.0.0.0",
            InstallLocation = @"C:\Program Files\WindowsApps\Example.Package_2.0.0.0_x64__publisherid",
            PackageFullName = "Example.Package_2.0.0.0_x64__publisherid",
            PackageManifestPath = @"C:\Program Files\WindowsApps\Example.Package_2.0.0.0_x64__publisherid\AppxManifest.xml"
        };

        var result = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId, before),
            InstalledApplicationTestData.Snapshot(sessionId, after),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.VersionChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.InstallLocationChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PackageFullNameChanged);
        Assert.Contains(result.Changes, change => change.Kind == InstalledApplicationChangeKind.PackageManifestPathChanged);
        Assert.All(result.Changes, change => Assert.NotEqual(InstalledApplicationChangeKind.ApplicationRegistered, change.Kind));
        Assert.All(result.Changes, change => Assert.NotEqual(InstalledApplicationChangeKind.ApplicationRemoved, change.Kind));
    }

    [Fact]
    public void CompareWarnsAndIgnoresDuplicateInstalledApplicationIdentities()
    {
        var sessionId = Guid.NewGuid();
        var first = InstalledApplicationTestData.AppxPackage("First Package");
        var duplicate = first with { DisplayName = "Duplicate Package" };

        var result = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId, first, duplicate),
            InstalledApplicationTestData.Snapshot(sessionId),
            DateTimeOffset.UtcNow);

        Assert.Single(result.Changes);
        Assert.Contains(result.Warnings, warning => warning.Contains("Duplicate installed application identity ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void CompareMarksUserScopeChangesWithoutPrivilegedLabel()
    {
        var sessionId = Guid.NewGuid();
        var application = InstalledApplicationTestData.Application(
            "User App",
            "UserApp",
            InstalledApplicationScopeKind.User,
            InstalledApplicationArchitectureKind.X86);

        var result = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId),
            InstalledApplicationTestData.Snapshot(sessionId, application),
            DateTimeOffset.UtcNow);

        var change = Assert.Single(result.Changes);
        Assert.DoesNotContain(ChangeAttentionLabel.Privileged, change.Labels);
        Assert.Contains(ChangeAttentionLabel.RollbackUnavailable, change.Labels);
    }

    [Fact]
    public void CompareRejectsSnapshotsFromDifferentSessions()
    {
        Assert.Throws<ArgumentException>(() => new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(Guid.NewGuid(), InstalledApplicationTestData.Application("App")),
            InstalledApplicationTestData.Snapshot(Guid.NewGuid(), InstalledApplicationTestData.Application("App")),
            DateTimeOffset.UtcNow));
    }
}
