using WinLedger.Core.Abstractions;
using WinLedger.Core.Firewall;
using WinLedger.Domain.Firewall;

namespace WinLedger.Windows.Firewall;

public sealed class WindowsFirewallSnapshotCollector(IClock clock) : IFirewallSnapshotCollector
{
    public Task<FirewallSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var rules = new List<WindowsFirewallRuleSnapshot>();
        var warnings = new List<string>();
        var occurrenceByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            dynamic policy = CreatePolicy();
            foreach (dynamic rule in policy.Rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string ruleName = rule.Name;
                    occurrenceByName.TryGetValue(ruleName, out var occurrence);
                    occurrenceByName[ruleName] = occurrence + 1;
                    rules.Add(WindowsFirewallRuleMapper.FromComRule(
                        rule,
                        WindowsFirewallRuleMapper.IdentityFor(rule, occurrence),
                        false));
                }
                catch (Exception ex) when (WindowsFirewallRuleMapper.IsFirewallException(ex))
                {
                    warnings.Add($"Firewall rule could not be read: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (WindowsFirewallRuleMapper.IsFirewallException(ex))
        {
            warnings.Add($"Firewall collection failed: {ex.Message}");
        }

        return Task.FromResult(new FirewallSnapshot(
            Guid.NewGuid(),
            sessionId,
            snapshotName,
            clock.UtcNow,
            WindowsFirewallRuleMapper.AnnotateDuplicateNames(rules),
            warnings.Distinct(StringComparer.Ordinal).ToArray()));
    }

    internal static object CreatePolicy()
    {
        var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2")
            ?? throw new InvalidOperationException("Windows Firewall COM policy is not available.");

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Windows Firewall COM policy could not be created.");
    }
}
