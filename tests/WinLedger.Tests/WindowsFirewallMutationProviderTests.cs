using WinLedger.Windows.Firewall;

namespace WinLedger.Tests;

public sealed class WindowsFirewallMutationProviderTests
{
    [Fact]
    public async Task ReadRulesByNameAsyncReturnsEmptyForMissingRule()
    {
        var provider = new WindowsFirewallMutationProvider();

        var rules = await provider.ReadRulesByNameAsync(
            $"WinLedger Missing Firewall Rule {Guid.NewGuid():N}",
            CancellationToken.None);

        Assert.Empty(rules);
    }
}
