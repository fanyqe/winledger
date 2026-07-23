using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WinLedger.Comparison.FileSystem;
using WinLedger.Core.Abstractions;
using WinLedger.Core.FileSystem;
using WinLedger.Core.Reports;
using WinLedger.Domain;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.FileSystem;
using WinLedger.Storage.Sqlite;

namespace WinLedger.Cli;

internal sealed class FileSystemCliCommands(IServiceProvider services)
{
    public async Task<int> CaptureAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger files-capture <database> <session-id> <snapshot-name> <root-path> [--hash] [--backup-small-files <bytes>] [--include-noise]");
            return 2;
        }

        var store = new SqliteWinLedgerStore(args[1]);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var sessionId = Guid.Parse(args[2]);
        var session = await store.GetSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        if (session is null)
        {
            Console.Error.WriteLine($"Session was not found: {sessionId}");
            return 3;
        }

        var options = ParseCaptureOptions(args);
        var collector = services.GetRequiredService<IFileSystemSnapshotCollector>();
        var snapshot = await collector.CaptureAsync(session.Id, args[3], options, CancellationToken.None)
            .ConfigureAwait(false);
        await store.SaveFileSystemSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"SessionId: {session.Id}");
        Console.WriteLine($"SnapshotId: {snapshot.Id}");
        Console.WriteLine($"Entries: {snapshot.Entries.Count}");
        Console.WriteLine($"Warnings: {snapshot.Warnings.Count}");
        return 0;
    }

    public async Task<int> CompareAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger files-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>");
            return 2;
        }

        var store = new SqliteWinLedgerStore(args[1]);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var baseline = await store.GetFileSystemSnapshotAsync(Guid.Parse(args[2]), CancellationToken.None)
            .ConfigureAwait(false);
        var comparison = await store.GetFileSystemSnapshotAsync(Guid.Parse(args[3]), CancellationToken.None)
            .ConfigureAwait(false);

        if (baseline is null || comparison is null)
        {
            Console.Error.WriteLine("One or both file-system snapshots could not be found.");
            return 3;
        }

        var comparer = services.GetRequiredService<FileSystemSnapshotComparer>();
        var clock = services.GetRequiredService<IClock>();
        var result = comparer.Compare(baseline, comparison, clock.UtcNow);
        var plan = services.GetRequiredService<FileSystemRollbackPlanner>().CreatePlan(result, clock.UtcNow);
        var exporter = services.GetRequiredService<FileSystemReportExporter>();
        var report = ReportOutputSelector.CreateReport(
            args[4],
            () => exporter.ExportJson(result, plan),
            () => exporter.ExportHtml(result, plan),
            () => exporter.ExportText(result, plan));

        await WriteReportAsync(args[4], report).ConfigureAwait(false);

        Console.WriteLine($"Changes: {result.Changes.Count}");
        Console.WriteLine($"RollbackOperations: {plan.Operations.Count}");
        Console.WriteLine($"ReportFormat: {ReportOutputSelector.FormatName(args[4])}");
        Console.WriteLine($"Report: {args[4]}");
        return 0;
    }

    public async Task<int> ApplyRollbackAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: winledger files-rollback-apply <report-json> <operation-id|all>");
            return 2;
        }

        var reportJson = await File.ReadAllTextAsync(args[1]).ConfigureAwait(false);
        var report = JsonSerializer.Deserialize<FileSystemRollbackReport>(reportJson, WinLedgerJsonSerializer.Options)
            ?? throw new InvalidOperationException("Rollback report could not be read.");

        var selectedIds = ParseSelectedOperationIds(args[2], report.RollbackPlan.Select(operation => operation.Id));
        var plan = new FileSystemRollbackPlan(Guid.NewGuid(), Guid.Empty, services.GetRequiredService<IClock>().UtcNow, report.RollbackPlan, []);
        var results = await services.GetRequiredService<FileSystemRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, CancellationToken.None)
            .ConfigureAwait(false);

        foreach (var result in results)
        {
            Console.WriteLine($"{result.OperationId}: {result.ValidationState} - {result.Message}");
        }

        return results.All(result => result.Succeeded) ? 0 : 4;
    }

    private static FileSystemSnapshotOptions ParseCaptureOptions(string[] args)
    {
        var calculateHashes = false;
        var backupSmallFiles = false;
        var backupSizeLimitBytes = 0L;
        var includeHighNoise = false;

        for (var index = 5; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--hash":
                    calculateHashes = true;
                    break;

                case "--include-noise":
                    includeHighNoise = true;
                    break;

                case "--backup-small-files":
                    if (index + 1 >= args.Length)
                    {
                        throw new ArgumentException("--backup-small-files requires a byte limit.");
                    }

                    backupSmallFiles = true;
                    backupSizeLimitBytes = long.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture);
                    break;

                default:
                    throw new ArgumentException($"Unknown files-capture option: {args[index]}");
            }
        }

        return new FileSystemSnapshotOptions(
            [args[4]],
            FileSystemSnapshotOptions.DefaultExclusionPatterns,
            includeHighNoise,
            calculateHashes,
            backupSmallFiles,
            backupSizeLimitBytes);
    }

    private static async Task WriteReportAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        await File.WriteAllTextAsync(path, content, ReportOutputSelector.GetEncoding(path)).ConfigureAwait(false);
    }

    private static HashSet<Guid> ParseSelectedOperationIds(string value, IEnumerable<Guid> allOperationIds)
    {
        return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? allOperationIds.ToHashSet()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .ToHashSet();
    }

    private sealed record FileSystemRollbackReport(IReadOnlyList<FileSystemRollbackOperation> RollbackPlan);
}
