namespace WinLedger.Domain.Firewall;

public sealed record WindowsFirewallRuleSnapshot(
    string Identity,
    string Name,
    string? Description,
    string? ApplicationPath,
    string? ServiceName,
    FirewallRuleProtocolKind Protocol,
    int ProtocolNumber,
    string? LocalPorts,
    string? RemotePorts,
    FirewallRuleDirectionKind Direction,
    FirewallRuleActionKind Action,
    bool Enabled,
    int Profiles,
    IReadOnlyList<string> ProfileNames,
    string? LocalAddresses,
    string? RemoteAddresses,
    string? InterfaceTypes,
    string? IcmpTypesAndCodes,
    bool EdgeTraversal,
    string? Grouping,
    bool HasDuplicateName);
