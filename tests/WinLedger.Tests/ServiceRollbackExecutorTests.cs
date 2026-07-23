using WinLedger.Core.Services;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Services;
using WinLedger.Rollback.Services;

namespace WinLedger.Tests;

public sealed class ServiceRollbackExecutorTests
{
    [Fact]
    public async Task ApplyAsyncStopsWhenCurrentServiceDoesNotMatchExpectedState()
    {
        var operation = Operation(
            expectedCurrentState: Service(ServiceStartModeKind.Disabled, true),
            restoreStartMode: ServiceStartModeKind.Automatic,
            restoreDelayedAutoStart: null,
            ServiceRollbackOperationKind.SetServiceStartMode);
        var provider = new InMemoryServiceMutationProvider
        {
            Current = Service(ServiceStartModeKind.Manual, true)
        };

        var result = await new ServiceRollbackExecutor(provider)
            .ApplyAsync(new ServiceRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    [Fact]
    public async Task ApplyAsyncStopsWhenNonTargetPersistentConfigurationChangedAgain()
    {
        var operation = Operation(
            expectedCurrentState: Service(ServiceStartModeKind.Disabled, true),
            restoreStartMode: ServiceStartModeKind.Automatic,
            restoreDelayedAutoStart: null,
            ServiceRollbackOperationKind.SetServiceStartMode);
        var provider = new InMemoryServiceMutationProvider
        {
            Current = Service(ServiceStartModeKind.Disabled, true) with { ExecutablePath = @"C:\Changed\service.exe" }
        };

        var result = await new ServiceRollbackExecutor(provider)
            .ApplyAsync(new ServiceRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    [Fact]
    public async Task ApplyAsyncRestoresStartModeAfterSuccessfulValidation()
    {
        var operation = Operation(
            expectedCurrentState: Service(ServiceStartModeKind.Disabled, true),
            restoreStartMode: ServiceStartModeKind.Automatic,
            restoreDelayedAutoStart: null,
            ServiceRollbackOperationKind.SetServiceStartMode);
        var provider = new InMemoryServiceMutationProvider
        {
            Current = Service(ServiceStartModeKind.Disabled, true)
        };

        var result = await new ServiceRollbackExecutor(provider)
            .ApplyAsync(new ServiceRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.Equal(ServiceStartModeKind.Automatic, provider.Current?.StartMode);
    }

    [Fact]
    public async Task ApplyAsyncRestoresDelayedAutoStartAfterSuccessfulValidation()
    {
        var operation = Operation(
            expectedCurrentState: Service(ServiceStartModeKind.Automatic, true),
            restoreStartMode: null,
            restoreDelayedAutoStart: false,
            ServiceRollbackOperationKind.SetServiceDelayedAutoStart);
        var provider = new InMemoryServiceMutationProvider
        {
            Current = Service(ServiceStartModeKind.Automatic, true)
        };

        var result = await new ServiceRollbackExecutor(provider)
            .ApplyAsync(new ServiceRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.False(provider.Current?.DelayedAutoStart);
    }

    private static ServiceRollbackOperation Operation(
        WindowsServiceSnapshot expectedCurrentState,
        ServiceStartModeKind? restoreStartMode,
        bool? restoreDelayedAutoStart,
        ServiceRollbackOperationKind kind)
    {
        return new ServiceRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            "ExampleService",
            expectedCurrentState,
            restoreStartMode,
            restoreDelayedAutoStart,
            true,
            true);
    }

    private static WindowsServiceSnapshot Service(ServiceStartModeKind startMode, bool delayedAutoStart)
    {
        return new WindowsServiceSnapshot(
            "ExampleService",
            "Example Service",
            startMode,
            @"C:\Example\service.exe",
            "LocalSystem",
            ServiceStateKind.Running,
            delayedAutoStart,
            [],
            null);
    }

    private sealed class InMemoryServiceMutationProvider : IServiceMutationProvider
    {
        public WindowsServiceSnapshot? Current { get; set; }

        public bool WasWritten { get; private set; }

        public Task<WindowsServiceSnapshot?> ReadServiceAsync(
            string serviceName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Current);
        }

        public Task SetStartModeAsync(
            string serviceName,
            ServiceStartModeKind startMode,
            CancellationToken cancellationToken)
        {
            Current = Current! with { StartMode = startMode };
            WasWritten = true;
            return Task.CompletedTask;
        }

        public Task SetDelayedAutoStartAsync(
            string serviceName,
            bool delayedAutoStart,
            CancellationToken cancellationToken)
        {
            Current = Current! with { DelayedAutoStart = delayedAutoStart };
            WasWritten = true;
            return Task.CompletedTask;
        }
    }
}
