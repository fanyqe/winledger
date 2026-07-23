using WinLedger.Core.Registry;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Registry;

namespace WinLedger.Tests;

public sealed class RegistryRollbackExecutorTests
{
    [Fact]
    public async Task ApplyAsyncStopsWhenCurrentValueDoesNotMatchExpectedState()
    {
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var operation = new RegistryRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            RollbackOperationKind.SetRegistryValue,
            keyPath,
            "Setting",
            Value("Setting", "\"after\"", "after"),
            Value("Setting", "\"before\"", "before"),
            false,
            false);
        var provider = new InMemoryRegistryMutationProvider
        {
            Current = Value("Setting", "\"changed-again\"", "changed-again")
        };

        var result = await new RegistryRollbackExecutor(provider)
            .ApplyAsync(new RegistryRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    [Fact]
    public async Task ApplyAsyncRestoresValueAfterSuccessfulValidation()
    {
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var operation = new RegistryRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            RollbackOperationKind.SetRegistryValue,
            keyPath,
            "Setting",
            Value("Setting", "\"after\"", "after"),
            Value("Setting", "\"before\"", "before"),
            false,
            false);
        var provider = new InMemoryRegistryMutationProvider
        {
            Current = Value("Setting", "\"after\"", "after")
        };

        var result = await new RegistryRollbackExecutor(provider)
            .ApplyAsync(new RegistryRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.Equal("\"before\"", provider.Current?.SerializedValue);
    }

    private static RegistryValueSnapshot Value(string name, string serialized, string display)
    {
        return new RegistryValueSnapshot(name, RegistryValueType.String, serialized, display);
    }

    private sealed class InMemoryRegistryMutationProvider : IRegistryMutationProvider
    {
        public RegistryValueSnapshot? Current { get; set; }

        public bool WasWritten { get; private set; }

        public Task<RegistryValueSnapshot?> ReadValueAsync(
            RegistryPath keyPath,
            string valueName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Current);
        }

        public Task SetValueAsync(
            RegistryPath keyPath,
            RegistryValueSnapshot value,
            CancellationToken cancellationToken)
        {
            Current = value;
            WasWritten = true;
            return Task.CompletedTask;
        }

        public Task DeleteValueAsync(
            RegistryPath keyPath,
            string valueName,
            CancellationToken cancellationToken)
        {
            Current = null;
            WasWritten = true;
            return Task.CompletedTask;
        }
    }
}
