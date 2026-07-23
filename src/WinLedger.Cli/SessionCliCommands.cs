using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using WinLedger.Core.Abstractions;
using WinLedger.Core.EnvironmentVariables;
using WinLedger.Core.FileSystem;
using WinLedger.Core.Firewall;
using WinLedger.Core.Hosts;
using WinLedger.Core.InstalledApplications;
using WinLedger.Core.Registry;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Core.Services;
using WinLedger.Core.Sessions;
using WinLedger.Core.Startup;
using WinLedger.Domain.Sessions;
using WinLedger.Storage.Sqlite;

namespace WinLedger.Cli;

internal sealed class SessionCliCommands(IServiceProvider services)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: winledger session <create|list|show|baseline|comparison> ...");
            return 2;
        }

        return args[1] switch
        {
            "create" => await CreateGroupedAsync(args).ConfigureAwait(false),
            "list" => await ListGroupedAsync(args).ConfigureAwait(false),
            "show" => await ShowGroupedAsync(args).ConfigureAwait(false),
            "baseline" => await BaselineGroupedAsync(args).ConfigureAwait(false),
            "comparison" => await ComparisonGroupedAsync(args).ConfigureAwait(false),
            _ => UnknownSessionCommand(args[1])
        };
    }

    public Task<int> CreateAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: winledger session-create <database> <session-title>");
            return Task.FromResult(2);
        }

        return CreateAsync(args[1], BuildTitle(args, 2));
    }

    public Task<int> ListAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: winledger session-list <database>");
            return Task.FromResult(2);
        }

        return ListAsync(args[1]);
    }

    public Task<int> ShowAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: winledger session-show <database> <session-id>");
            return Task.FromResult(2);
        }

        return ShowAsync(args[1], args[2]);
    }

    public Task<int> BaselineAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: winledger session-baseline <database> <session-id> <snapshot-name> [options]");
            return Task.FromResult(2);
        }

        return CaptureAsync(args[1], args[2], args[3], TrackingSnapshotStage.Baseline, args.Skip(4).ToArray());
    }

    public Task<int> ComparisonAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: winledger session-comparison <database> <session-id> <snapshot-name> [options]");
            return Task.FromResult(2);
        }

        return CaptureAsync(args[1], args[2], args[3], TrackingSnapshotStage.Comparison, args.Skip(4).ToArray());
    }

    private Task<int> CreateGroupedAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: winledger session create <database> <session-title>");
            return Task.FromResult(2);
        }

        return CreateAsync(args[2], BuildTitle(args, 3));
    }

    private Task<int> ListGroupedAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: winledger session list <database>");
            return Task.FromResult(2);
        }

        return ListAsync(args[2]);
    }

    private Task<int> ShowGroupedAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: winledger session show <database> <session-id>");
            return Task.FromResult(2);
        }

        return ShowAsync(args[2], args[3]);
    }

    private Task<int> BaselineGroupedAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger session baseline <database> <session-id> <snapshot-name> [options]");
            return Task.FromResult(2);
        }

        return CaptureAsync(args[2], args[3], args[4], TrackingSnapshotStage.Baseline, args.Skip(5).ToArray());
    }

    private Task<int> ComparisonGroupedAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: winledger session comparison <database> <session-id> <snapshot-name> [options]");
            return Task.FromResult(2);
        }

        return CaptureAsync(args[2], args[3], args[4], TrackingSnapshotStage.Comparison, args.Skip(5).ToArray());
    }

    private async Task<int> CreateAsync(string databasePath, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            Console.Error.WriteLine("Session title is required.");
            return 2;
        }

        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var clock = services.GetRequiredService<IClock>();
        var session = new TrackingSession(
            Guid.NewGuid(),
            title.Trim(),
            null,
            clock.UtcNow,
            Environment.OSVersion.VersionString,
            RuntimeInformation.ProcessArchitecture.ToString(),
            HashUserSid(),
            IsAdministrator(),
            TrackingSessionStatus.Created);

        await store.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"SessionId: {session.Id}");
        return 0;
    }

    private static async Task<int> ListAsync(string databasePath)
    {
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var sessions = await store.ListSessionsAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"Sessions: {sessions.Count}");
        if (sessions.Count == 0)
        {
            return 0;
        }

        Console.WriteLine("Id | CreatedAtUtc | Status | Title");
        foreach (var session in sessions)
        {
            Console.WriteLine($"{session.Id} | {session.CreatedAt.ToUniversalTime():O} | {session.Status} | {ToSingleLine(session.Title)}");
        }

        return 0;
    }

    private static async Task<int> ShowAsync(string databasePath, string sessionIdValue)
    {
        var sessionId = Guid.Parse(sessionIdValue);
        var store = new SqliteWinLedgerStore(databasePath);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var session = await store.GetSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        if (session is null)
        {
            Console.Error.WriteLine($"Session was not found: {sessionId}");
            return 3;
        }

        Console.WriteLine($"SessionId: {session.Id}");
        Console.WriteLine($"Title: {ToSingleLine(session.Title)}");
        Console.WriteLine($"Description: {ToSingleLine(session.Description ?? "(none)")}");
        Console.WriteLine($"CreatedAtUtc: {session.CreatedAt.ToUniversalTime():O}");
        Console.WriteLine($"WindowsVersion: {ToSingleLine(session.WindowsVersion)}");
        Console.WriteLine($"Architecture: {session.Architecture}");
        Console.WriteLine($"UserSidHash: {session.UserSidHash}");
        Console.WriteLine($"IsAdministrator: {session.IsAdministrator}");
        Console.WriteLine($"Status: {session.Status}");
        return 0;
    }

    private async Task<int> CaptureAsync(
        string databasePath,
        string sessionIdValue,
        string snapshotName,
        TrackingSnapshotStage stage,
        IReadOnlyList<string> options)
    {
        var parsedOptions = SessionCaptureOptionsParser.Parse(options);
        var store = new SqliteWinLedgerStore(databasePath);
        var orchestrator = CreateCaptureOrchestrator(store);
        var sessionId = Guid.Parse(sessionIdValue);

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;

        try
        {
            var result = await orchestrator.CaptureAsync(
                new TrackingSessionCaptureRequest(
                    sessionId,
                    snapshotName,
                    stage,
                    parsedOptions.Subsystems,
                    parsedOptions.RegistryTargets,
                    parsedOptions.FileSystemOptions),
                ConsoleCaptureProgress.Instance,
                cancellation.Token).ConfigureAwait(false);

            Console.WriteLine($"SessionId: {result.Session.Id}");
            Console.WriteLine($"Stage: {result.Stage}");
            Console.WriteLine($"Status: {result.Session.Status}");
            Console.WriteLine($"Snapshots: {result.Snapshots.Count}");
            Console.WriteLine("Subsystem | SnapshotId | Items | Warnings");
            foreach (var snapshot in result.Snapshots)
            {
                Console.WriteLine($"{snapshot.Subsystem} | {snapshot.SnapshotId} | {snapshot.ItemCount} | {snapshot.WarningCount}");
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation canceled.");
            return 4;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static string BuildTitle(string[] args, int startIndex)
    {
        return string.Join(' ', args.Skip(startIndex));
    }

    private static int UnknownSessionCommand(string command)
    {
        Console.Error.WriteLine($"Unknown session command: {command}");
        Console.Error.WriteLine("Usage: winledger session <create|list|show|baseline|comparison> ...");
        return 2;
    }

    private TrackingSessionCaptureOrchestrator CreateCaptureOrchestrator(SqliteWinLedgerStore store)
    {
        return new TrackingSessionCaptureOrchestrator(
            store,
            services.GetRequiredService<IRegistrySnapshotCollector>(),
            store,
            services.GetRequiredService<IServiceSnapshotCollector>(),
            store,
            services.GetRequiredService<IScheduledTaskSnapshotCollector>(),
            store,
            services.GetRequiredService<IStartupSnapshotCollector>(),
            store,
            services.GetRequiredService<IEnvironmentSnapshotCollector>(),
            store,
            services.GetRequiredService<IHostsFileSnapshotCollector>(),
            store,
            services.GetRequiredService<IFirewallSnapshotCollector>(),
            store,
            services.GetRequiredService<IInstalledApplicationSnapshotCollector>(),
            store,
            services.GetRequiredService<IFileSystemSnapshotCollector>(),
            store);
    }

    private static void PrintCaptureProgress(TrackingSessionCaptureProgress progress)
    {
        Console.WriteLine(
            $"{progress.CompletedSubsystems}/{progress.TotalSubsystems} {progress.Subsystem}: {progress.Message}");
    }

    private sealed class ConsoleCaptureProgress : IProgress<TrackingSessionCaptureProgress>
    {
        public static readonly ConsoleCaptureProgress Instance = new();

        public void Report(TrackingSessionCaptureProgress value)
        {
            PrintCaptureProgress(value);
        }
    }

    private static string ToSingleLine(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static string HashUserSid()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? "unknown";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sid));
        return Convert.ToHexString(bytes);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

}
