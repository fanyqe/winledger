using WinLedger.Core.Firewall;
using WinLedger.Domain.Firewall;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Firewall;

namespace WinLedger.Tests;

public sealed class FirewallRollbackExecutorTests
{
    [Fact]
    public async Task ApplyAsyncStopsWhenRuleChangedAfterComparison()
    {
        var expected = FirewallTestData.Rule("Existing rule", enabled: true);
        var operation = Operation(FirewallRollbackOperationKind.SetFirewallRuleEnabled, expected, false);
        var provider = new InMemoryFirewallMutationProvider
        {
            Rules = [expected with { LocalPorts = "8080" }]
        };

        var result = await new FirewallRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    [Fact]
    public async Task ApplyAsyncStopsWhenDuplicateNamesExist()
    {
        var expected = FirewallTestData.Rule("Duplicate rule", enabled: true);
        var operation = Operation(FirewallRollbackOperationKind.SetFirewallRuleEnabled, expected, false);
        var provider = new InMemoryFirewallMutationProvider
        {
            Rules = [expected, expected with { Identity = "Duplicate rule\u001F1" }]
        };

        var result = await new FirewallRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    [Fact]
    public async Task ApplyAsyncDeletesCreatedRuleAfterSuccessfulValidation()
    {
        var expected = FirewallTestData.Rule("Created rule");
        var operation = Operation(FirewallRollbackOperationKind.DeleteFirewallRule, expected, null);
        var provider = new InMemoryFirewallMutationProvider
        {
            Rules = [expected]
        };

        var result = await new FirewallRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.Empty(provider.Rules);
    }

    [Fact]
    public async Task ApplyAsyncRestoresEnabledStateAfterSuccessfulValidation()
    {
        var expected = FirewallTestData.Rule("Existing rule", enabled: true);
        var operation = Operation(FirewallRollbackOperationKind.SetFirewallRuleEnabled, expected, false);
        var provider = new InMemoryFirewallMutationProvider
        {
            Rules = [expected]
        };

        var result = await new FirewallRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.False(Assert.Single(provider.Rules).Enabled);
    }

    [Fact]
    public async Task ApplyAsyncFailsWhenRestoreValueIsMissing()
    {
        var expected = FirewallTestData.Rule("Existing rule", enabled: true);
        var operation = Operation(FirewallRollbackOperationKind.SetFirewallRuleEnabled, expected, null);
        var provider = new InMemoryFirewallMutationProvider
        {
            Rules = [expected]
        };

        var result = await new FirewallRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Failed, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    private static FirewallRollbackPlan Plan(FirewallRollbackOperation operation)
    {
        return new FirewallRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []);
    }

    private static FirewallRollbackOperation Operation(
        FirewallRollbackOperationKind kind,
        WindowsFirewallRuleSnapshot expected,
        bool? restoreEnabled)
    {
        return new FirewallRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            expected.Name,
            expected,
            restoreEnabled,
            true,
            false);
    }

    private sealed class InMemoryFirewallMutationProvider : IFirewallMutationProvider
    {
        public IReadOnlyList<WindowsFirewallRuleSnapshot> Rules { get; set; } = [];

        public bool WasWritten { get; private set; }

        public Task<IReadOnlyList<WindowsFirewallRuleSnapshot>> ReadRulesByNameAsync(
            string ruleName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WindowsFirewallRuleSnapshot>>(
                Rules.Where(rule => string.Equals(rule.Name, ruleName, StringComparison.OrdinalIgnoreCase)).ToArray());
        }

        public Task DeleteRuleAsync(
            string ruleName,
            CancellationToken cancellationToken)
        {
            Rules = Rules.Where(rule => !string.Equals(rule.Name, ruleName, StringComparison.OrdinalIgnoreCase)).ToArray();
            WasWritten = true;
            return Task.CompletedTask;
        }

        public Task SetRuleEnabledAsync(
            string ruleName,
            bool enabled,
            CancellationToken cancellationToken)
        {
            Rules = Rules.Select(rule => string.Equals(rule.Name, ruleName, StringComparison.OrdinalIgnoreCase)
                ? rule with { Enabled = enabled }
                : rule).ToArray();
            WasWritten = true;
            return Task.CompletedTask;
        }
    }
}
