using WinLedger.Domain.Firewall;

namespace WinLedger.Core.Firewall;

public interface IFirewallMutationProvider
{
    Task<IReadOnlyList<WindowsFirewallRuleSnapshot>> ReadRulesByNameAsync(
        string ruleName,
        CancellationToken cancellationToken);

    Task DeleteRuleAsync(
        string ruleName,
        CancellationToken cancellationToken);

    Task SetRuleEnabledAsync(
        string ruleName,
        bool enabled,
        CancellationToken cancellationToken);
}
