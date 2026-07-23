using WinLedger.Comparison.Firewall;
using WinLedger.Domain.Firewall;
using WinLedger.Domain.Rollback;

namespace WinLedger.Tests;

public sealed class FirewallSnapshotComparerTests
{
    [Fact]
    public void CompareDetectsCreatedRemovedAndEnabledChanges()
    {
        var sessionId = Guid.NewGuid();
        var removed = FirewallTestData.Rule("Removed rule");
        var before = FirewallTestData.Rule("Existing rule", enabled: false);
        var after = before with { Enabled = true };
        var created = FirewallTestData.Rule("Created rule");

        var result = new FirewallSnapshotComparer().Compare(
            FirewallTestData.Snapshot(sessionId, removed, before),
            FirewallTestData.Snapshot(sessionId, after, created),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change =>
            change.Kind == FirewallRuleChangeKind.RuleCreated &&
            change.RuleName == "Created rule" &&
            change.RollbackAvailability == RollbackAvailability.RequiresConfirmation &&
            change.Labels.Contains(ChangeAttentionLabel.NetworkRelated) &&
            change.Labels.Contains(ChangeAttentionLabel.SecuritySensitive));
        Assert.Contains(result.Changes, change =>
            change.Kind == FirewallRuleChangeKind.RuleRemoved &&
            change.RuleName == "Removed rule" &&
            change.RollbackAvailability == RollbackAvailability.ManualReview);
        Assert.Contains(result.Changes, change =>
            change.Kind == FirewallRuleChangeKind.EnabledChanged &&
            change.RuleName == "Existing rule" &&
            change.RollbackAvailability == RollbackAvailability.RequiresConfirmation);
    }

    [Fact]
    public void CompareDetectsFirewallRulePropertyChanges()
    {
        var sessionId = Guid.NewGuid();
        var before = FirewallTestData.Rule("Changed rule");
        var after = before with
        {
            Action = FirewallRuleActionKind.Block,
            Direction = FirewallRuleDirectionKind.Outbound,
            ApplicationPath = @"C:\Example\client.exe",
            ServiceName = "ExampleService",
            Protocol = FirewallRuleProtocolKind.Udp,
            ProtocolNumber = 17,
            LocalPorts = "53",
            RemotePorts = "5353",
            Profiles = 6,
            ProfileNames = ["Private", "Public"],
            LocalAddresses = "LocalSubnet",
            RemoteAddresses = "10.0.0.0/24",
            InterfaceTypes = "Wireless",
            EdgeTraversal = true,
            Description = "Updated",
            Grouping = "Updated group"
        };

        var result = new FirewallSnapshotComparer().Compare(
            FirewallTestData.Snapshot(sessionId, before),
            FirewallTestData.Snapshot(sessionId, after),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.ActionChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.DirectionChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.ApplicationPathChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.ServiceNameChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.ProtocolChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.PortsChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.ProfilesChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.AddressesChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.InterfaceTypesChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.EdgeTraversalChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.DescriptionChanged);
        Assert.Contains(result.Changes, change => change.Kind == FirewallRuleChangeKind.GroupingChanged);
    }

    [Fact]
    public void CompareMarksDuplicateRuleNamesForManualReview()
    {
        var sessionId = Guid.NewGuid();
        var created = FirewallTestData.Rule("Duplicate rule", duplicateName: true);

        var result = new FirewallSnapshotComparer().Compare(
            FirewallTestData.Snapshot(sessionId),
            FirewallTestData.Snapshot(sessionId, created),
            DateTimeOffset.UtcNow);

        var change = Assert.Single(result.Changes);
        Assert.Equal(RollbackAvailability.ManualReview, change.RollbackAvailability);
        Assert.Contains(ChangeAttentionLabel.RollbackUnavailable, change.Labels);
    }

    [Fact]
    public void CompareRejectsSnapshotsFromDifferentSessions()
    {
        Assert.Throws<ArgumentException>(() => new FirewallSnapshotComparer().Compare(
            FirewallTestData.Snapshot(Guid.NewGuid(), FirewallTestData.Rule("Rule")),
            FirewallTestData.Snapshot(Guid.NewGuid(), FirewallTestData.Rule("Rule")),
            DateTimeOffset.UtcNow));
    }
}
