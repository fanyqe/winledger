using WinLedger.Collectors.Registry;
using WinLedger.Domain.Registry;

namespace WinLedger.Tests;

public sealed class DefaultRegistrySnapshotTargetsTests
{
    [Fact]
    public void InstallerProfileIncludesMultipleHivesAndRegistryViews()
    {
        var targets = DefaultRegistrySnapshotTargets.InstallerProfile.Targets;

        Assert.Contains(targets, target => target.Path.Hive == RegistryHiveKind.CurrentUser);
        Assert.Contains(targets, target => target.Path.Hive == RegistryHiveKind.LocalMachine);
        Assert.Contains(targets, target => target.Path.View == RegistryViewKind.Registry32);
        Assert.Contains(targets, target => target.Path.View == RegistryViewKind.Registry64);
        Assert.Contains(targets, target => target.Path.KeyPath.Contains(@"SYSTEM\CurrentControlSet\Services", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveProfileAcceptsDefaultAlias()
    {
        var profile = DefaultRegistrySnapshotTargets.ResolveProfile("default");

        Assert.Same(DefaultRegistrySnapshotTargets.DefaultProfile, profile);
    }

    [Fact]
    public void NormalizeTargetsRemovesDuplicatePathsWithSameView()
    {
        var target = new RegistrySnapshotTarget(
            new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox"),
            IncludeSubKeys: true);

        var normalized = DefaultRegistrySnapshotTargets.NormalizeTargets([target, target]);

        Assert.Single(normalized);
    }
}
