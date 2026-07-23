namespace WinLedger.Domain.Firewall;

public sealed record FirewallSnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    IReadOnlyList<WindowsFirewallRuleSnapshot> Rules,
    IReadOnlyList<string> Warnings);
