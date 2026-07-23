using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WinLedger.Collectors.Registry;
using WinLedger.Comparison.Registry;
using WinLedger.Comparison.Services;
using WinLedger.Core.Abstractions;
using WinLedger.Core.Reports;
using WinLedger.Core.Registry;
using WinLedger.Core.Services;
using WinLedger.Domain;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Registry;
using WinLedger.Rollback.Services;
using WinLedger.Storage.Sqlite;

namespace WinLedger.Cli;

internal sealed class CliApplication(IServiceProvider services)
{
    private readonly SessionCliCommands sessionCommands = new(services);
    private readonly ScheduledTaskCliCommands scheduledTaskCommands = new(services);
    private readonly StartupCliCommands startupCommands = new(services);
    private readonly EnvironmentCliCommands environmentCommands = new(services);
    private readonly HostsFileCliCommands hostsFileCommands = new(services);
    private readonly FirewallCliCommands firewallCommands = new(services);
    private readonly InstalledApplicationCliCommands installedApplicationCommands = new(services);
    private readonly FileSystemCliCommands fileSystemCommands = new(services);
    private readonly ElevatedRollbackCliCommands elevatedRollbackCommands = new(services);

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "session" => await sessionCommands.RunAsync(args).ConfigureAwait(false),
                "session-create" => await sessionCommands.CreateAsync(args).ConfigureAwait(false),
                "session-list" => await sessionCommands.ListAsync(args).ConfigureAwait(false),
                "session-show" => await sessionCommands.ShowAsync(args).ConfigureAwait(false),
                "session-baseline" => await sessionCommands.BaselineAsync(args).ConfigureAwait(false),
                "session-comparison" => await sessionCommands.ComparisonAsync(args).ConfigureAwait(false),
                "session-cleanup" => await sessionCommands.CleanupAsync(args).ConfigureAwait(false),
                "registry-capture" => await CaptureRegistryAsync(args).ConfigureAwait(false),
                "registry-compare" => await CompareRegistryAsync(args).ConfigureAwait(false),
                "registry-rollback-apply" => await ApplyRegistryRollbackAsync(args).ConfigureAwait(false),
                "service-capture" => await CaptureServicesAsync(args).ConfigureAwait(false),
                "service-compare" => await CompareServicesAsync(args).ConfigureAwait(false),
                "service-rollback-apply" => await ApplyServiceRollbackAsync(args).ConfigureAwait(false),
                "task-capture" => await scheduledTaskCommands.CaptureAsync(args).ConfigureAwait(false),
                "task-compare" => await scheduledTaskCommands.CompareAsync(args).ConfigureAwait(false),
                "task-rollback-apply" => await scheduledTaskCommands.ApplyRollbackAsync(args).ConfigureAwait(false),
                "startup-capture" => await startupCommands.CaptureAsync(args).ConfigureAwait(false),
                "startup-compare" => await startupCommands.CompareAsync(args).ConfigureAwait(false),
                "startup-rollback-apply" => await startupCommands.ApplyRollbackAsync(args).ConfigureAwait(false),
                "environment-capture" => await environmentCommands.CaptureAsync(args).ConfigureAwait(false),
                "environment-compare" => await environmentCommands.CompareAsync(args).ConfigureAwait(false),
                "environment-rollback-apply" => await environmentCommands.ApplyRollbackAsync(args).ConfigureAwait(false),
                "hosts-capture" => await hostsFileCommands.CaptureAsync(args).ConfigureAwait(false),
                "hosts-compare" => await hostsFileCommands.CompareAsync(args).ConfigureAwait(false),
                "hosts-rollback-apply" => await hostsFileCommands.ApplyRollbackAsync(args).ConfigureAwait(false),
                "firewall-capture" => await firewallCommands.CaptureAsync(args).ConfigureAwait(false),
                "firewall-compare" => await firewallCommands.CompareAsync(args).ConfigureAwait(false),
                "firewall-rollback-apply" => await firewallCommands.ApplyRollbackAsync(args).ConfigureAwait(false),
                "applications-capture" => await installedApplicationCommands.CaptureAsync(args).ConfigureAwait(false),
                "applications-compare" => await installedApplicationCommands.CompareAsync(args).ConfigureAwait(false),
                "files-capture" => await fileSystemCommands.CaptureAsync(args).ConfigureAwait(false),
                "files-compare" => await fileSystemCommands.CompareAsync(args).ConfigureAwait(false),
                "files-rollback-apply" => await fileSystemCommands.ApplyRollbackAsync(args).ConfigureAwait(false),
                "elevated-rollback-apply" => await elevatedRollbackCommands.ApplyAsync(args).ConfigureAwait(false),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> CaptureRegistryAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger registry-capture <database> <session-id> <snapshot-name> <registry-path|--sandbox|--profile profile-name> [additional-registry-path...]");
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

        var collector = services.GetRequiredService<IRegistrySnapshotCollector>();
        var targets = ParseRegistryCaptureTargets(args.Skip(4).ToArray());

        var snapshot = await collector.CaptureAsync(session.Id, args[3], targets, CancellationToken.None)
            .ConfigureAwait(false);
        await store.SaveRegistrySnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"SessionId: {session.Id}");
        Console.WriteLine($"SnapshotId: {snapshot.Id}");
        Console.WriteLine($"Keys: {snapshot.Keys.Count}");
        Console.WriteLine($"Warnings: {snapshot.Warnings.Count}");
        return 0;
    }

    private static IReadOnlyList<RegistrySnapshotTarget> ParseRegistryCaptureTargets(IReadOnlyList<string> args)
    {
        var targets = new List<RegistrySnapshotTarget>();
        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index].ToLowerInvariant())
            {
                case "--sandbox":
                    targets.AddRange(DefaultRegistrySnapshotTargets.MinimalSandboxTargets);
                    break;

                case "--profile":
                    if (index + 1 >= args.Count)
                    {
                        throw new ArgumentException("--profile requires a registry profile name.");
                    }

                    targets.AddRange(DefaultRegistrySnapshotTargets.ResolveProfile(args[++index]).Targets);
                    break;

                default:
                    targets.Add(new RegistrySnapshotTarget(RegistryPath.Parse(args[index]), true));
                    break;
            }
        }

        return DefaultRegistrySnapshotTargets.NormalizeTargets(targets);
    }

    private async Task<int> CompareRegistryAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger registry-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>");
            return 2;
        }

        var store = new SqliteWinLedgerStore(args[1]);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var baseline = await store.GetRegistrySnapshotAsync(Guid.Parse(args[2]), CancellationToken.None)
            .ConfigureAwait(false);
        var comparison = await store.GetRegistrySnapshotAsync(Guid.Parse(args[3]), CancellationToken.None)
            .ConfigureAwait(false);

        if (baseline is null || comparison is null)
        {
            Console.Error.WriteLine("One or both registry snapshots could not be found.");
            return 3;
        }

        var comparer = services.GetRequiredService<RegistrySnapshotComparer>();
        var clock = services.GetRequiredService<IClock>();
        var result = comparer.Compare(baseline, comparison, clock.UtcNow);
        var plan = services.GetRequiredService<RegistryRollbackPlanner>().CreatePlan(result, clock.UtcNow);
        var exporter = services.GetRequiredService<RegistryReportExporter>();
        var report = ReportOutputSelector.CreateReport(
            args[4],
            () => exporter.ExportJson(result, plan),
            () => exporter.ExportHtml(result, plan),
            () => exporter.ExportText(result, plan),
            () => exporter.ExportReg(result, plan),
            () => exporter.ExportPowerShell(result, plan));

        await WriteReportAsync(args[4], report, registryEditorSupported: true).ConfigureAwait(false);

        Console.WriteLine($"Changes: {result.Changes.Count}");
        Console.WriteLine($"RollbackOperations: {plan.Operations.Count}");
        Console.WriteLine($"ReportFormat: {ReportOutputSelector.FormatName(args[4], registryEditorSupported: true, powerShellSupported: true)}");
        Console.WriteLine($"Report: {args[4]}");
        return 0;
    }

    private async Task<int> ApplyRegistryRollbackAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: winledger registry-rollback-apply <report-json> <operation-id|all>");
            return 2;
        }

        var reportJson = await File.ReadAllTextAsync(args[1]).ConfigureAwait(false);
        var report = JsonSerializer.Deserialize<RegistryRollbackReport>(reportJson, WinLedgerJsonSerializer.Options)
            ?? throw new InvalidOperationException("Rollback report could not be read.");

        var selectedIds = ParseSelectedOperationIds(args[2], report.RollbackPlan.Select(operation => operation.Id));
        var plan = new RegistryRollbackPlan(Guid.NewGuid(), Guid.Empty, services.GetRequiredService<IClock>().UtcNow, report.RollbackPlan, []);
        var results = await services.GetRequiredService<RegistryRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, CancellationToken.None)
            .ConfigureAwait(false);

        PrintRollbackResults(results.Select(result => (result.OperationId, result.Succeeded, result.ValidationState.ToString(), result.Message)));
        return results.All(result => result.Succeeded) ? 0 : 4;
    }

    private async Task<int> CaptureServicesAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: winledger service-capture <database> <session-id> <snapshot-name>");
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

        var collector = services.GetRequiredService<IServiceSnapshotCollector>();
        var snapshot = await collector.CaptureAsync(session.Id, args[3], CancellationToken.None)
            .ConfigureAwait(false);
        await store.SaveServiceSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"SessionId: {session.Id}");
        Console.WriteLine($"SnapshotId: {snapshot.Id}");
        Console.WriteLine($"Services: {snapshot.Services.Count}");
        Console.WriteLine($"Warnings: {snapshot.Warnings.Count}");
        return 0;
    }

    private async Task<int> CompareServicesAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger service-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>");
            return 2;
        }

        var store = new SqliteWinLedgerStore(args[1]);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var baseline = await store.GetServiceSnapshotAsync(Guid.Parse(args[2]), CancellationToken.None)
            .ConfigureAwait(false);
        var comparison = await store.GetServiceSnapshotAsync(Guid.Parse(args[3]), CancellationToken.None)
            .ConfigureAwait(false);

        if (baseline is null || comparison is null)
        {
            Console.Error.WriteLine("One or both service snapshots could not be found.");
            return 3;
        }

        var comparer = services.GetRequiredService<ServiceSnapshotComparer>();
        var clock = services.GetRequiredService<IClock>();
        var result = comparer.Compare(baseline, comparison, clock.UtcNow);
        var plan = services.GetRequiredService<ServiceRollbackPlanner>().CreatePlan(result, clock.UtcNow);
        var exporter = services.GetRequiredService<ServiceReportExporter>();
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

    private async Task<int> ApplyServiceRollbackAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: winledger service-rollback-apply <report-json> <operation-id|all>");
            return 2;
        }

        var reportJson = await File.ReadAllTextAsync(args[1]).ConfigureAwait(false);
        var report = JsonSerializer.Deserialize<ServiceRollbackReport>(reportJson, WinLedgerJsonSerializer.Options)
            ?? throw new InvalidOperationException("Rollback report could not be read.");

        var selectedIds = ParseSelectedOperationIds(args[2], report.RollbackPlan.Select(operation => operation.Id));
        var plan = new ServiceRollbackPlan(Guid.NewGuid(), Guid.Empty, services.GetRequiredService<IClock>().UtcNow, report.RollbackPlan, []);
        var results = await services.GetRequiredService<ServiceRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, CancellationToken.None)
            .ConfigureAwait(false);

        PrintRollbackResults(results.Select(result => (result.OperationId, result.Succeeded, result.ValidationState.ToString(), result.Message)));
        return results.All(result => result.Succeeded) ? 0 : 4;
    }

    private static async Task WriteReportAsync(string path, string content, bool registryEditorSupported = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        await File.WriteAllTextAsync(
            path,
            content,
            ReportOutputSelector.GetEncoding(path, registryEditorSupported)).ConfigureAwait(false);
    }

    private static HashSet<Guid> ParseSelectedOperationIds(string value, IEnumerable<Guid> allOperationIds)
    {
        return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? allOperationIds.ToHashSet()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .ToHashSet();
    }

    private static void PrintRollbackResults(IEnumerable<(Guid OperationId, bool Succeeded, string ValidationState, string Message)> results)
    {
        foreach (var result in results)
        {
            Console.WriteLine($"{result.OperationId}: {result.ValidationState} - {result.Message}");
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            WinLedger command line preview

            Commands:
              winledger session create <database> <session-title>
              winledger session list <database>
              winledger session show <database> <session-id>
              winledger session baseline <database> <session-id> <snapshot-name> [options]
              winledger session comparison <database> <session-id> <snapshot-name> [options]
              winledger session cleanup <database> (--older-than-days <days>|--before <utc-iso>) [--keep-newest <count>] [--dry-run]
              winledger session-create <database> <session-title>
              winledger session-list <database>
              winledger session-show <database> <session-id>
              winledger session-baseline <database> <session-id> <snapshot-name> [options]
              winledger session-comparison <database> <session-id> <snapshot-name> [options]
              winledger session-cleanup <database> (--older-than-days <days>|--before <utc-iso>) [--keep-newest <count>] [--dry-run]
              winledger registry-capture <database> <session-id> <snapshot-name> <registry-path|--sandbox|--profile profile-name> [additional-registry-path...]
              winledger registry-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger registry-rollback-apply <report-json> <operation-id|all>
              winledger service-capture <database> <session-id> <snapshot-name>
              winledger service-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger service-rollback-apply <report-json> <operation-id|all>
              winledger task-capture <database> <session-id> <snapshot-name>
              winledger task-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger task-rollback-apply <report-json> <operation-id|all>
              winledger startup-capture <database> <session-id> <snapshot-name>
              winledger startup-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger startup-rollback-apply <report-json> <operation-id|all>
              winledger environment-capture <database> <session-id> <snapshot-name>
              winledger environment-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger environment-rollback-apply <report-json> <operation-id|all>
              winledger hosts-capture <database> <session-id> <snapshot-name>
              winledger hosts-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger hosts-rollback-apply <report-json> <operation-id|all>
              winledger firewall-capture <database> <session-id> <snapshot-name>
              winledger firewall-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger firewall-rollback-apply <report-json> <operation-id|all>
              winledger applications-capture <database> <session-id> <snapshot-name>
              winledger applications-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger files-capture <database> <session-id> <snapshot-name> <root-path> [--hash|--no-hash] [--backup-small-files <bytes>] [--include-noise]
              winledger files-compare <database> <baseline-snapshot-id> <comparison-snapshot-id> <report-output>
              winledger files-rollback-apply <report-json> <operation-id|all>
              winledger elevated-rollback-apply <subsystem> <report-json> <operation-id|all> [helper-exe] [--no-elevation]

            Report outputs use JSON by default, HTML for .html or .htm, plain text for .txt or .text, and registry rollback .reg or .ps1 for registry reports.

            Session capture options:
              --subsystems <names>                 Comma-separated list: all, registry, services, tasks, startup, environment, hosts, firewall, applications, files
              --registry-profile <name>            Built-in registry profile: installer, user, machine, startup, policy, sandbox
              --registry-path <path>               Add a recursive registry target. Can be used more than once.
              --registry-sandbox                   Capture the built-in sandbox registry targets.
              --files-root <path>                  Add a monitored file-system root. Can be used more than once.
              --hash                               Calculate file hashes. This is enabled by default.
              --no-hash                            Skip file hashes for faster file-system capture.
              --backup-small-files <bytes>         Store small-file backup content up to the byte limit.
              --include-noise                      Include high-noise file-system paths.
              --older-than-days <days>             Cleanup sessions older than the given age.
              --before <utc-iso>                   Cleanup sessions created before the given UTC timestamp.
              --keep-newest <count>                Keep the newest sessions even if they are older than the cutoff.
              --dry-run                            Report cleanup counts without deleting records.

            Examples:
              winledger session create .\winledger.db "Installing ExampleApp"
              winledger session list .\winledger.db
              winledger session show .\winledger.db <session-id>
              winledger session baseline .\winledger.db <session-id> Baseline --registry-profile installer --files-root .\Sandbox
              winledger session comparison .\winledger.db <session-id> Comparison --registry-profile installer --files-root .\Sandbox
              winledger session cleanup .\winledger.db --older-than-days 30 --keep-newest 10 --dry-run
              winledger service-capture .\winledger.db <session-id> Baseline
              winledger task-capture .\winledger.db <session-id> Baseline
              winledger startup-capture .\winledger.db <session-id> Baseline
              winledger environment-capture .\winledger.db <session-id> Baseline
              winledger hosts-capture .\winledger.db <session-id> Baseline
              winledger firewall-capture .\winledger.db <session-id> Baseline
              winledger applications-capture .\winledger.db <session-id> Baseline
              winledger files-capture .\winledger.db <session-id> Baseline .\Sandbox --backup-small-files 262144
            """);
    }

    private sealed record RegistryRollbackReport(IReadOnlyList<RegistryRollbackOperation> RollbackPlan);

    private sealed record ServiceRollbackReport(IReadOnlyList<ServiceRollbackOperation> RollbackPlan);
}
