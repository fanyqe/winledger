using WinLedger.Core.EnvironmentVariables;
using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.EnvironmentVariables;

namespace WinLedger.Tests;

public sealed class EnvironmentRollbackExecutorTests
{
    [Fact]
    public async Task ApplyAsyncStopsWhenVariableChangedAfterComparison()
    {
        var expected = Variable("USER_VAR", "after", EnvironmentVariableScopeKind.User);
        var operation = Operation(EnvironmentRollbackOperationKind.SetEnvironmentVariable, expected, expected with { RawValue = "before" });
        var provider = new InMemoryEnvironmentMutationProvider
        {
            Current = expected with { RawValue = "changed-again" }
        };

        var result = await new EnvironmentRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    [Fact]
    public async Task ApplyAsyncRestoresVariableAfterSuccessfulValidation()
    {
        var expected = Variable("USER_VAR", "after", EnvironmentVariableScopeKind.User);
        var restore = expected with { RawValue = "before" };
        var operation = Operation(EnvironmentRollbackOperationKind.SetEnvironmentVariable, expected, restore);
        var provider = new InMemoryEnvironmentMutationProvider
        {
            Current = expected
        };

        var result = await new EnvironmentRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.Equal("before", provider.Current?.RawValue);
    }

    [Fact]
    public async Task ApplyAsyncDeletesCreatedVariableAfterSuccessfulValidation()
    {
        var expected = Variable("CREATED_VAR", "new", EnvironmentVariableScopeKind.User);
        var operation = Operation(EnvironmentRollbackOperationKind.DeleteEnvironmentVariable, expected, null);
        var provider = new InMemoryEnvironmentMutationProvider
        {
            Current = expected
        };

        var result = await new EnvironmentRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.Null(provider.Current);
    }

    [Fact]
    public async Task ApplyAsyncFailsWhenRestoreValueIsMissing()
    {
        var expected = Variable("USER_VAR", "after", EnvironmentVariableScopeKind.User);
        var operation = Operation(EnvironmentRollbackOperationKind.SetEnvironmentVariable, expected, null);
        var provider = new InMemoryEnvironmentMutationProvider
        {
            Current = expected
        };

        var result = await new EnvironmentRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Failed, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    private static EnvironmentRollbackPlan Plan(EnvironmentRollbackOperation operation)
    {
        return new EnvironmentRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []);
    }

    private static EnvironmentRollbackOperation Operation(
        EnvironmentRollbackOperationKind kind,
        EnvironmentVariableSnapshot? expected,
        EnvironmentVariableSnapshot? restore)
    {
        var source = expected ?? restore ?? throw new ArgumentException("Expected or restore value is required.", nameof(expected));
        return new EnvironmentRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            source.Scope,
            source.Name,
            expected,
            restore,
            source.Scope == EnvironmentVariableScopeKind.Machine,
            true);
    }

    private static EnvironmentVariableSnapshot Variable(
        string name,
        string value,
        EnvironmentVariableScopeKind scope)
    {
        return new EnvironmentVariableSnapshot(
            scope,
            name,
            value,
            EnvironmentVariableValueType.ExpandString,
            string.Equals(name, "Path", StringComparison.OrdinalIgnoreCase)
                ? value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>(),
            scope == EnvironmentVariableScopeKind.User
                ? @"HKCU\Environment"
                : @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
    }

    private sealed class InMemoryEnvironmentMutationProvider : IEnvironmentMutationProvider
    {
        public EnvironmentVariableSnapshot? Current { get; set; }

        public bool WasWritten { get; private set; }

        public Task<EnvironmentVariableSnapshot?> ReadVariableAsync(
            EnvironmentVariableScopeKind scope,
            string name,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Current);
        }

        public Task SetVariableAsync(
            EnvironmentVariableSnapshot variable,
            CancellationToken cancellationToken)
        {
            Current = variable;
            WasWritten = true;
            return Task.CompletedTask;
        }

        public Task DeleteVariableAsync(
            EnvironmentVariableScopeKind scope,
            string name,
            CancellationToken cancellationToken)
        {
            Current = null;
            WasWritten = true;
            return Task.CompletedTask;
        }
    }
}
