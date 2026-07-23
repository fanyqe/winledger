using System.Text;
using WinLedger.Core.FileSystem;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.FileSystem;

namespace WinLedger.Tests;

public sealed class FileSystemRollbackExecutorTests
{
    [Fact]
    public async Task ApplyAsyncDeletesCreatedEntryAfterSuccessfulValidation()
    {
        var created = FileSystemTestData.File("created.txt");
        var operation = Operation(
            FileSystemRollbackOperationKind.DeleteCreatedEntry,
            created.Path,
            FileSystemEntryKind.File,
            created,
            null);
        var provider = new InMemoryFileSystemMutationProvider { Current = created };

        var result = await new FileSystemRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(result.Single().Succeeded);
        Assert.True(provider.Deleted);
    }

    [Fact]
    public async Task ApplyAsyncRestoresDeletedFileWhenCurrentEntryIsStillMissing()
    {
        var restore = FileSystemTestData.File("deleted.txt", hasRollbackData: true, content: "restore");
        var operation = Operation(
            FileSystemRollbackOperationKind.RestoreDeletedFile,
            restore.Path,
            FileSystemEntryKind.File,
            null,
            restore);
        var provider = new InMemoryFileSystemMutationProvider { Current = null };

        var result = await new FileSystemRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.True(result.Single().Succeeded);
        Assert.Equal("restore", provider.RestoredText);
    }

    [Fact]
    public async Task ApplyAsyncStopsWhenCurrentEntryChangedAfterComparison()
    {
        var expected = FileSystemTestData.File("modified.txt", sha256: "EXPECTED");
        var current = expected with { Sha256 = "CURRENT" };
        var operation = Operation(
            FileSystemRollbackOperationKind.RestoreModifiedFile,
            expected.Path,
            FileSystemEntryKind.File,
            expected,
            expected with { BackupContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("restore")), HasRollbackData = true });
        var provider = new InMemoryFileSystemMutationProvider { Current = current };

        var result = await new FileSystemRollbackExecutor(provider)
            .ApplyAsync(Plan(operation), new HashSet<Guid> { operation.Id }, CancellationToken.None);

        Assert.False(result.Single().Succeeded);
        Assert.Equal(RollbackValidationState.Conflict, result.Single().ValidationState);
        Assert.False(provider.Restored);
    }

    private static FileSystemRollbackPlan Plan(FileSystemRollbackOperation operation)
    {
        return new FileSystemRollbackPlan(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, [operation], []);
    }

    private static FileSystemRollbackOperation Operation(
        FileSystemRollbackOperationKind kind,
        string targetPath,
        FileSystemEntryKind entryKind,
        FileSystemEntrySnapshot? expectedCurrentEntry,
        FileSystemEntrySnapshot? restoreEntry)
    {
        return new FileSystemRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            FileSystemTestData.Root,
            targetPath,
            entryKind,
            expectedCurrentEntry,
            restoreEntry,
            restoreEntry?.BackupContentBase64,
            false,
            false);
    }

    private sealed class InMemoryFileSystemMutationProvider : IFileSystemMutationProvider
    {
        public FileSystemEntrySnapshot? Current { get; set; }

        public bool Deleted { get; private set; }

        public bool Restored { get; private set; }

        public string? RestoredText { get; private set; }

        public Task<FileSystemEntrySnapshot?> ReadEntryAsync(
            string rootPath,
            string path,
            bool calculateHash,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Current);
        }

        public Task DeleteEntryAsync(
            string rootPath,
            string path,
            FileSystemEntryKind kind,
            CancellationToken cancellationToken)
        {
            Deleted = true;
            Current = null;
            return Task.CompletedTask;
        }

        public Task RestoreFileContentAsync(
            string rootPath,
            string path,
            string contentBase64,
            DateTimeOffset? lastWriteTimeUtc,
            CancellationToken cancellationToken)
        {
            Restored = true;
            RestoredText = Encoding.UTF8.GetString(Convert.FromBase64String(contentBase64));
            Current = FileSystemTestData.File(System.IO.Path.GetFileName(path), hasRollbackData: true, content: RestoredText);
            return Task.CompletedTask;
        }
    }
}
