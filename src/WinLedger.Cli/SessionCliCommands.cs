using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using WinLedger.Core.Abstractions;
using WinLedger.Domain.Sessions;
using WinLedger.Storage.Sqlite;

namespace WinLedger.Cli;

internal sealed class SessionCliCommands(IServiceProvider services)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: winledger session <create|list|show> ...");
            return 2;
        }

        return args[1] switch
        {
            "create" => await CreateGroupedAsync(args).ConfigureAwait(false),
            "list" => await ListGroupedAsync(args).ConfigureAwait(false),
            "show" => await ShowGroupedAsync(args).ConfigureAwait(false),
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

    private static string BuildTitle(string[] args, int startIndex)
    {
        return string.Join(' ', args.Skip(startIndex));
    }

    private static int UnknownSessionCommand(string command)
    {
        Console.Error.WriteLine($"Unknown session command: {command}");
        Console.Error.WriteLine("Usage: winledger session <create|list|show> ...");
        return 2;
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
