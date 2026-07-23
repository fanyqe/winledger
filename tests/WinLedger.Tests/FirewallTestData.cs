using WinLedger.Domain.Firewall;

namespace WinLedger.Tests;

internal static class FirewallTestData
{
    public static FirewallSnapshot Snapshot(Guid sessionId, params WindowsFirewallRuleSnapshot[] rules)
    {
        return new FirewallSnapshot(
            Guid.NewGuid(),
            sessionId,
            "Firewall",
            DateTimeOffset.UtcNow,
            rules,
            []);
    }

    public static WindowsFirewallRuleSnapshot Rule(
        string name,
        bool enabled = true,
        FirewallRuleDirectionKind direction = FirewallRuleDirectionKind.Inbound,
        FirewallRuleActionKind action = FirewallRuleActionKind.Allow,
        FirewallRuleProtocolKind protocol = FirewallRuleProtocolKind.Tcp,
        int protocolNumber = 6,
        string? localPorts = "80",
        string? applicationPath = @"C:\Example\server.exe",
        int profiles = 2,
        bool duplicateName = false)
    {
        return new WindowsFirewallRuleSnapshot(
            name,
            name,
            "Test firewall rule",
            applicationPath,
            null,
            protocol,
            protocolNumber,
            localPorts,
            "*",
            direction,
            action,
            enabled,
            profiles,
            ProfileNames(profiles),
            "*",
            "*",
            "All",
            null,
            false,
            "WinLedger",
            duplicateName);
    }

    private static IReadOnlyList<string> ProfileNames(int profiles)
    {
        var names = new List<string>();
        if ((profiles & 1) != 0)
        {
            names.Add("Domain");
        }

        if ((profiles & 2) != 0)
        {
            names.Add("Private");
        }

        if ((profiles & 4) != 0)
        {
            names.Add("Public");
        }

        return names;
    }
}
