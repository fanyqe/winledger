using WinLedger.Core.Startup;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Startup;
using WinLedger.Rollback.Startup;

namespace WinLedger.Tests;

public sealed class StartupRollbackExecutorTests
{
    [Fact]
    public async Task ApplyAsyncStopsWhenStartupFolderEntryChangedAfterComparison()
    {
        var expected = Entry(@"C:\Startup\Created.lnk", fileSize: 42);
        var operation = Operation(expected);
        var provider = new InMemoryStartupMutationProvider
        {
            Current = expected with { FileSize = 43 }
        };

        var result = await new StartupRollbackExecutor(provider)
            .ApplyAsync(new StartupRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        var single = Assert.Single(result);
        Assert.False(single.Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, single.ValidationState);
        Assert.False(provider.WasDeleted);
    }

    [Fact]
    public async Task ApplyAsyncDeletesCreatedStartupFolderEntryAfterSuccessfulValidation()
    {
        var expected = Entry(@"C:\Startup\Created.lnk", fileSize: 42);
        var operation = Operation(expected);
        var provider = new InMemoryStartupMutationProvider
        {
            Current = expected
        };

        var result = await new StartupRollbackExecutor(provider)
            .ApplyAsync(new StartupRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.True(provider.WasDeleted);
        Assert.Null(provider.Current);
    }

    [Fact]
    public async Task ValidateAsyncRejectsNonStartupFolderSources()
    {
        var operation = Operation(Entry(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Example", StartupEntrySourceKind.RegistryRun));
        var provider = new InMemoryStartupMutationProvider();

        var result = await new StartupRollbackExecutor(provider).ValidateAsync(operation, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RollbackValidationState.Failed, result.ValidationState);
        Assert.False(provider.WasRead);
    }

    private static StartupRollbackOperation Operation(StartupEntrySnapshot expected)
    {
        return new StartupRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StartupRollbackOperationKind.DeleteStartupFolderEntry,
            expected,
            false,
            false);
    }

    private static StartupEntrySnapshot Entry(
        string location,
        StartupEntrySourceKind source = StartupEntrySourceKind.StartupFolder,
        long? fileSize = null)
    {
        return new StartupEntrySnapshot(
            $"{source}|{location}",
            source,
            Path.GetFileName(location),
            location,
            location,
            true,
            null,
            "Startup folder entry",
            source.ToString(),
            fileSize,
            fileSize is null ? null : DateTimeOffset.UnixEpoch);
    }

    private sealed class InMemoryStartupMutationProvider : IStartupMutationProvider
    {
        public StartupEntrySnapshot? Current { get; set; }

        public bool WasDeleted { get; private set; }

        public bool WasRead { get; private set; }

        public Task<StartupEntrySnapshot?> ReadStartupFolderEntryAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            WasRead = true;
            return Task.FromResult(Current);
        }

        public Task DeleteStartupFolderEntryAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            WasDeleted = true;
            Current = null;
            return Task.CompletedTask;
        }
    }
}
