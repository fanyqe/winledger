using WinLedger.Core.Hosts;
using WinLedger.Domain.Hosts;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Hosts;

namespace WinLedger.Tests;

public sealed class HostsFileRollbackExecutorTests
{
    [Fact]
    public async Task ApplyAsyncStopsWhenHostsFileChangedAfterComparison()
    {
        var expected = HostsFileTestData.Snapshot(Guid.NewGuid(), "Expected", "127.0.0.1 localhost\r\n10.0.0.2 new.example\r\n");
        var restore = HostsFileTestData.Snapshot(expected.SessionId, "Restore", "127.0.0.1 localhost\r\n");
        var operation = Operation(HostsFileRollbackOperationKind.RestoreHostsFileContent, expected, restore.ContentBase64);
        var provider = new InMemoryHostsFileMutationProvider
        {
            Current = HostsFileTestData.Snapshot(expected.SessionId, "Current", "127.0.0.1 localhost\r\n10.0.0.3 changed.example\r\n")
        };

        var result = await new HostsFileRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    [Fact]
    public async Task ApplyAsyncRestoresHostsFileAfterSuccessfulValidation()
    {
        var expected = HostsFileTestData.Snapshot(Guid.NewGuid(), "Expected", "127.0.0.1 localhost\r\n10.0.0.2 new.example\r\n");
        var restore = HostsFileTestData.Snapshot(expected.SessionId, "Restore", "127.0.0.1 localhost\r\n");
        var operation = Operation(HostsFileRollbackOperationKind.RestoreHostsFileContent, expected, restore.ContentBase64);
        var provider = new InMemoryHostsFileMutationProvider
        {
            Current = expected
        };

        var result = await new HostsFileRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.Equal(restore.ContentBase64, provider.Current?.ContentBase64);
    }

    [Fact]
    public async Task ApplyAsyncDeletesCreatedHostsFileAfterSuccessfulValidation()
    {
        var expected = HostsFileTestData.Snapshot(Guid.NewGuid(), "Expected", "127.0.0.1 localhost\r\n");
        var operation = Operation(HostsFileRollbackOperationKind.DeleteHostsFile, expected, null);
        var provider = new InMemoryHostsFileMutationProvider
        {
            Current = expected
        };

        var result = await new HostsFileRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasWritten);
        Assert.NotNull(provider.Current);
        Assert.False(provider.Current.Exists);
    }

    [Fact]
    public async Task ApplyAsyncFailsWhenRestoreContentIsMissing()
    {
        var expected = HostsFileTestData.Snapshot(Guid.NewGuid(), "Expected", "127.0.0.1 localhost\r\n");
        var operation = Operation(HostsFileRollbackOperationKind.RestoreHostsFileContent, expected, null);
        var provider = new InMemoryHostsFileMutationProvider
        {
            Current = expected
        };

        var result = await new HostsFileRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Failed, single.ValidationState);
        Assert.False(provider.WasWritten);
    }

    private static HostsFileRollbackPlan Plan(HostsFileRollbackOperation operation)
    {
        return new HostsFileRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []);
    }

    private static HostsFileRollbackOperation Operation(
        HostsFileRollbackOperationKind kind,
        HostsFileSnapshot expected,
        string? restoreContentBase64)
    {
        return new HostsFileRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            expected.FilePath,
            expected,
            restoreContentBase64,
            true,
            false);
    }

    private sealed class InMemoryHostsFileMutationProvider : IHostsFileMutationProvider
    {
        public HostsFileSnapshot Current { get; set; } =
            HostsFileTestData.Missing(Guid.Empty, "Current");

        public bool WasWritten { get; private set; }

        public Task<HostsFileSnapshot> ReadSnapshotAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Current);
        }

        public Task RestoreContentAsync(
            string filePath,
            string contentBase64,
            CancellationToken cancellationToken)
        {
            var content = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(contentBase64));
            Current = HostsFileTestData.Snapshot(Current.SessionId, "Current", content, filePath);
            WasWritten = true;
            return Task.CompletedTask;
        }

        public Task DeleteFileAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            Current = HostsFileTestData.Missing(Current.SessionId, "Current", filePath);
            WasWritten = true;
            return Task.CompletedTask;
        }
    }
}
