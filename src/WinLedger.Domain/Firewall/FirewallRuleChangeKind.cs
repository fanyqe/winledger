namespace WinLedger.Domain.Firewall;

public enum FirewallRuleChangeKind
{
    RuleCreated,
    RuleRemoved,
    EnabledChanged,
    ActionChanged,
    DirectionChanged,
    ApplicationPathChanged,
    ServiceNameChanged,
    ProtocolChanged,
    PortsChanged,
    ProfilesChanged,
    AddressesChanged,
    InterfaceTypesChanged,
    EdgeTraversalChanged,
    DescriptionChanged,
    GroupingChanged
}
