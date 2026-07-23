using Microsoft.Extensions.DependencyInjection;
using WinLedger.Comparison.InstalledApplications;
using WinLedger.Core.Abstractions;
using WinLedger.Core.InstalledApplications;
using WinLedger.Core.Reports;
using WinLedger.Rollback.InstalledApplications;
using WinLedger.Storage.Sqlite;

namespace WinLedger.Cli;

internal sealed class InstalledApplicationCliCommands(IServiceProvider services)
{
    public async Task<int> CaptureAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: winledger applications-capture <database> <session-id> <snapshot-name>");
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

        var collector = services.GetRequiredService<IInstalledApplicationSnapshotCollector>();
        var snapshot = await collector.CaptureAsync(session.Id, args[3], CancellationToken.None)
            .ConfigureAwait(false);
        await store.SaveInstalledApplicationsSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"SessionId: {session.Id}");
        Console.WriteLine($"SnapshotId: {snapshot.Id}");
        Console.WriteLine($"Applications: {snapshot.Applications.Count}");
        Console.WriteLine($"Warnings: {snapshot.Warnings.Count}");
        return 0;
    }

    public async Task<int> CompareAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger applications-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>");
            return 2;
        }

        var store = new SqliteWinLedgerStore(args[1]);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var baseline = await store.GetInstalledApplicationsSnapshotAsync(Guid.Parse(args[2]), CancellationToken.None)
            .ConfigureAwait(false);
        var comparison = await store.GetInstalledApplicationsSnapshotAsync(Guid.Parse(args[3]), CancellationToken.None)
            .ConfigureAwait(false);

        if (baseline is null || comparison is null)
        {
            Console.Error.WriteLine("One or both installed application snapshots could not be found.");
            return 3;
        }

        var comparer = services.GetRequiredService<InstalledApplicationsSnapshotComparer>();
        var clock = services.GetRequiredService<IClock>();
        var result = comparer.Compare(baseline, comparison, clock.UtcNow);
        var plan = services.GetRequiredService<InstalledApplicationRollbackPlanner>().CreatePlan(result, clock.UtcNow);
        var exporter = services.GetRequiredService<InstalledApplicationReportExporter>();
        var report = ReportOutputSelector.CreateReport(
            args[4],
            () => exporter.ExportJson(result, plan),
            () => exporter.ExportHtml(result, plan),
            () => exporter.ExportText(result, plan));

        await WriteReportAsync(args[4], report).ConfigureAwait(false);

        Console.WriteLine($"Changes: {result.Changes.Count}");
        Console.WriteLine($"RollbackOperations: {plan.Operations.Count}");
        Console.WriteLine($"ManualReviewWarnings: {plan.Warnings.Count}");
        Console.WriteLine($"ReportFormat: {ReportOutputSelector.FormatName(args[4])}");
        Console.WriteLine($"Report: {args[4]}");
        return 0;
    }

    private static async Task WriteReportAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        await File.WriteAllTextAsync(path, content, ReportOutputSelector.GetEncoding(path)).ConfigureAwait(false);
    }
}
