using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WinLedger.Comparison.EnvironmentVariables;
using WinLedger.Core.Abstractions;
using WinLedger.Core.EnvironmentVariables;
using WinLedger.Core.Reports;
using WinLedger.Domain;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.EnvironmentVariables;
using WinLedger.Storage.Sqlite;

namespace WinLedger.Cli;

internal sealed class EnvironmentCliCommands(IServiceProvider services)
{
    public async Task<int> CaptureAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: winledger environment-capture <database> <session-id> <snapshot-name>");
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

        var collector = services.GetRequiredService<IEnvironmentSnapshotCollector>();
        var snapshot = await collector.CaptureAsync(session.Id, args[3], CancellationToken.None)
            .ConfigureAwait(false);
        await store.SaveEnvironmentSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"SessionId: {session.Id}");
        Console.WriteLine($"SnapshotId: {snapshot.Id}");
        Console.WriteLine($"EnvironmentVariables: {snapshot.Variables.Count}");
        Console.WriteLine($"Warnings: {snapshot.Warnings.Count}");
        return 0;
    }

    public async Task<int> CompareAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger environment-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>");
            return 2;
        }

        var store = new SqliteWinLedgerStore(args[1]);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var baseline = await store.GetEnvironmentSnapshotAsync(Guid.Parse(args[2]), CancellationToken.None)
            .ConfigureAwait(false);
        var comparison = await store.GetEnvironmentSnapshotAsync(Guid.Parse(args[3]), CancellationToken.None)
            .ConfigureAwait(false);

        if (baseline is null || comparison is null)
        {
            Console.Error.WriteLine("One or both environment snapshots could not be found.");
            return 3;
        }

        var comparer = services.GetRequiredService<EnvironmentSnapshotComparer>();
        var clock = services.GetRequiredService<IClock>();
        var result = comparer.Compare(baseline, comparison, clock.UtcNow);
        var plan = services.GetRequiredService<EnvironmentRollbackPlanner>().CreatePlan(result, clock.UtcNow);
        var exporter = services.GetRequiredService<EnvironmentReportExporter>();
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
            Console.Error.WriteLine("Usage: winledger environment-rollback-apply <report-json> <operation-id|all>");
            return 2;
        }

        var reportJson = await File.ReadAllTextAsync(args[1]).ConfigureAwait(false);
        var report = JsonSerializer.Deserialize<EnvironmentRollbackReport>(reportJson, WinLedgerJsonSerializer.Options)
            ?? throw new InvalidOperationException("Rollback report could not be read.");

        var selectedIds = ParseSelectedOperationIds(args[2], report.RollbackPlan.Select(operation => operation.Id));
        var plan = new EnvironmentRollbackPlan(Guid.NewGuid(), Guid.Empty, services.GetRequiredService<IClock>().UtcNow, report.RollbackPlan, []);
        var results = await services.GetRequiredService<EnvironmentRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, CancellationToken.None)
            .ConfigureAwait(false);

        foreach (var result in results)
        {
            Console.WriteLine($"{result.OperationId}: {result.ValidationState} - {result.Message}");
        }

        return results.All(result => result.Succeeded) ? 0 : 4;
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

    private sealed record EnvironmentRollbackReport(IReadOnlyList<EnvironmentRollbackOperation> RollbackPlan);
}
