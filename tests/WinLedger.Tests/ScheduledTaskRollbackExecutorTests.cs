using WinLedger.Core.ScheduledTasks;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.ScheduledTasks;
using WinLedger.Rollback.ScheduledTasks;

namespace WinLedger.Tests;

public sealed class ScheduledTaskRollbackExecutorTests
{
    [Fact]
    public async Task ApplyAsyncStopsWhenTaskDefinitionChangedAfterComparison()
    {
        var operation = Operation(
            ScheduledTaskRollbackOperationKind.DeleteScheduledTask,
            TaskSnapshot(@"\WinLedger\CreatedTask", true),
            null);
        var provider = new InMemoryScheduledTaskMutationProvider
        {
            Current = TaskSnapshot(@"\WinLedger\CreatedTask", true) with { DefinitionXml = "<Task changed=\"true\" />" }
        };

        var result = await new ScheduledTaskRollbackExecutor(provider)
            .ApplyAsync(new ScheduledTaskRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    [Fact]
    public async Task ApplyAsyncDeletesCreatedTaskAfterSuccessfulValidation()
    {
        var expected = TaskSnapshot(@"\WinLedger\CreatedTask", true);
        var operation = Operation(ScheduledTaskRollbackOperationKind.DeleteScheduledTask, expected, null);
        var provider = new InMemoryScheduledTaskMutationProvider
        {
            Current = expected
        };

        var result = await new ScheduledTaskRollbackExecutor(provider)
            .ApplyAsync(new ScheduledTaskRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.Null(provider.Current);
    }

    [Fact]
    public async Task ApplyAsyncRestoresEnabledStateAfterSuccessfulValidation()
    {
        var expected = TaskSnapshot(@"\WinLedger\ExistingTask", true);
        var operation = Operation(ScheduledTaskRollbackOperationKind.SetScheduledTaskEnabled, expected, false);
        var provider = new InMemoryScheduledTaskMutationProvider
        {
            Current = expected
        };

        var result = await new ScheduledTaskRollbackExecutor(provider)
            .ApplyAsync(new ScheduledTaskRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.False(provider.Current?.Enabled);
    }

    private static ScheduledTaskRollbackOperation Operation(
        ScheduledTaskRollbackOperationKind kind,
        ScheduledTaskDefinitionSnapshot expected,
        bool? restoreEnabled)
    {
        return new ScheduledTaskRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            expected.FullPath,
            expected,
            restoreEnabled,
            true,
            false);
    }

    private static ScheduledTaskDefinitionSnapshot TaskSnapshot(string path, bool enabled)
    {
        return new ScheduledTaskDefinitionSnapshot(
            path,
            @"\WinLedger",
            path.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last(),
            enabled,
            enabled ? ScheduledTaskStateKind.Ready : ScheduledTaskStateKind.Disabled,
            "SYSTEM",
            ScheduledTaskPrivilegeLevelKind.HighestAvailable,
            [new ScheduledTaskActionSnapshot(ScheduledTaskActionKind.Execute, @"C:\Example\task.exe", null, null, @"C:\Example\task.exe")],
            [new ScheduledTaskTriggerSnapshot(ScheduledTaskTriggerKind.Logon, true, null, null, "Logon trigger enabled")],
            $"<Task path=\"{path}\" enabled=\"{enabled}\" />");
    }

    private sealed class InMemoryScheduledTaskMutationProvider : IScheduledTaskMutationProvider
    {
        public ScheduledTaskDefinitionSnapshot? Current { get; set; }

        public bool WasWritten { get; private set; }

        public Task<ScheduledTaskDefinitionSnapshot?> ReadTaskAsync(
            string taskPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Current);
        }

        public Task DeleteTaskAsync(
            string taskPath,
            CancellationToken cancellationToken)
        {
            Current = null;
            WasWritten = true;
            return Task.CompletedTask;
        }

        public Task SetEnabledAsync(
            string taskPath,
            bool enabled,
            CancellationToken cancellationToken)
        {
            Current = Current! with { Enabled = enabled };
            WasWritten = true;
            return Task.CompletedTask;
        }
    }
}
