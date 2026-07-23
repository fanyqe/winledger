using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WinLedger.Core.Abstractions;
using WinLedger.Core.Elevation;
using WinLedger.Domain;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.EnvironmentVariables;
using WinLedger.Rollback.FileSystem;
using WinLedger.Rollback.Firewall;
using WinLedger.Rollback.Hosts;
using WinLedger.Rollback.Registry;
using WinLedger.Rollback.ScheduledTasks;
using WinLedger.Rollback.Services;
using WinLedger.Rollback.Startup;

namespace WinLedger.ElevatedHelper;

internal sealed class ElevatedHelperApplication(IServiceProvider services)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string? responsePath = null;
        ElevatedRollbackRequest? request = null;
        try
        {
            var paths = ParseArgs(args);
            responsePath = paths.ResponsePath;
            request = await ReadRequestAsync(paths.RequestPath, CancellationToken.None).ConfigureAwait(false);
            var response = await HandleRequestAsync(request, CancellationToken.None).ConfigureAwait(false);
            await WriteResponseAsync(responsePath, response, CancellationToken.None).ConfigureAwait(false);
            return response.Succeeded ? 0 : 4;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            if (request is not null && responsePath is not null)
            {
                await WriteResponseAsync(
                    responsePath,
                    Failure(request.RequestId, true, ex.Message),
                    CancellationToken.None).ConfigureAwait(false);
            }

            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private async Task<ElevatedRollbackResponse> HandleRequestAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ProtocolVersion, ElevatedHelperProtocol.Version, StringComparison.Ordinal))
        {
            return Failure(request.RequestId, true, $"Unsupported elevated helper protocol version: {request.ProtocolVersion}");
        }

        var token = Environment.GetEnvironmentVariable(ElevatedHelperProtocol.AuthenticationTokenEnvironmentVariable);
        if (token is null || !ElevatedHelperAuthenticator.Matches(token, request.AuthenticationTokenSha256))
        {
            return Failure(request.RequestId, false, "Elevated helper authentication failed.");
        }

        var auditLogger = services.GetRequiredService<ElevatedHelperAuditLogger>();
        await auditLogger.WriteAsync(
            request.RequestId,
            $"accepted subsystem={request.Subsystem} selector={request.OperationSelector}",
            cancellationToken).ConfigureAwait(false);

        var results = request.Subsystem switch
        {
            ElevatedRollbackSubsystem.Registry => await ApplyRegistryAsync(request, cancellationToken).ConfigureAwait(false),
            ElevatedRollbackSubsystem.Services => await ApplyServicesAsync(request, cancellationToken).ConfigureAwait(false),
            ElevatedRollbackSubsystem.ScheduledTasks => await ApplyScheduledTasksAsync(request, cancellationToken).ConfigureAwait(false),
            ElevatedRollbackSubsystem.Startup => await ApplyStartupAsync(request, cancellationToken).ConfigureAwait(false),
            ElevatedRollbackSubsystem.Environment => await ApplyEnvironmentAsync(request, cancellationToken).ConfigureAwait(false),
            ElevatedRollbackSubsystem.HostsFile => await ApplyHostsFileAsync(request, cancellationToken).ConfigureAwait(false),
            ElevatedRollbackSubsystem.Firewall => await ApplyFirewallAsync(request, cancellationToken).ConfigureAwait(false),
            ElevatedRollbackSubsystem.FileSystem => await ApplyFileSystemAsync(request, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Subsystem, "Unsupported rollback subsystem.")
        };

        var response = new ElevatedRollbackResponse(
            request.RequestId,
            true,
            results.Count > 0 && results.All(result => result.Succeeded),
            results,
            []);

        await auditLogger.WriteAsync(
            request.RequestId,
            $"completed succeeded={response.Succeeded} operations={results.Count}",
            cancellationToken).ConfigureAwait(false);

        return response;
    }

    private async Task<IReadOnlyList<ElevatedRollbackOperationResult>> ApplyRegistryAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var report = await ReadReportAsync<RegistryRollbackReport>(request.ReportJsonPath, cancellationToken).ConfigureAwait(false);
        var selectedIds = ParseSelectedOperationIds(request.OperationSelector, report.RollbackPlan.Select(operation => operation.Id));
        var plan = new RegistryRollbackPlan(
            Guid.NewGuid(),
            Guid.Empty,
            services.GetRequiredService<IClock>().UtcNow,
            report.RollbackPlan,
            []);
        var results = await services.GetRequiredService<RegistryRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, cancellationToken).ConfigureAwait(false);
        return results.Select(result => new ElevatedRollbackOperationResult(result.OperationId, result.Succeeded, result.ValidationState, result.Message)).ToArray();
    }

    private async Task<IReadOnlyList<ElevatedRollbackOperationResult>> ApplyServicesAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var report = await ReadReportAsync<ServiceRollbackReport>(request.ReportJsonPath, cancellationToken).ConfigureAwait(false);
        var selectedIds = ParseSelectedOperationIds(request.OperationSelector, report.RollbackPlan.Select(operation => operation.Id));
        var plan = new ServiceRollbackPlan(
            Guid.NewGuid(),
            Guid.Empty,
            services.GetRequiredService<IClock>().UtcNow,
            report.RollbackPlan,
            []);
        var results = await services.GetRequiredService<ServiceRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, cancellationToken).ConfigureAwait(false);
        return results.Select(result => new ElevatedRollbackOperationResult(result.OperationId, result.Succeeded, result.ValidationState, result.Message)).ToArray();
    }

    private async Task<IReadOnlyList<ElevatedRollbackOperationResult>> ApplyScheduledTasksAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var report = await ReadReportAsync<ScheduledTaskRollbackReport>(request.ReportJsonPath, cancellationToken).ConfigureAwait(false);
        var selectedIds = ParseSelectedOperationIds(request.OperationSelector, report.RollbackPlan.Select(operation => operation.Id));
        var plan = new ScheduledTaskRollbackPlan(
            Guid.NewGuid(),
            Guid.Empty,
            services.GetRequiredService<IClock>().UtcNow,
            report.RollbackPlan,
            []);
        var results = await services.GetRequiredService<ScheduledTaskRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, cancellationToken).ConfigureAwait(false);
        return results.Select(result => new ElevatedRollbackOperationResult(result.OperationId, result.Succeeded, result.ValidationState, result.Message)).ToArray();
    }

    private async Task<IReadOnlyList<ElevatedRollbackOperationResult>> ApplyStartupAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var report = await ReadReportAsync<StartupRollbackReport>(request.ReportJsonPath, cancellationToken).ConfigureAwait(false);
        var selectedIds = ParseSelectedOperationIds(request.OperationSelector, report.RollbackPlan.Select(operation => operation.Id));
        var plan = new StartupRollbackPlan(
            Guid.NewGuid(),
            Guid.Empty,
            services.GetRequiredService<IClock>().UtcNow,
            report.RollbackPlan,
            []);
        var results = await services.GetRequiredService<StartupRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, cancellationToken).ConfigureAwait(false);
        return results.Select(result => new ElevatedRollbackOperationResult(result.OperationId, result.Succeeded, result.ValidationState, result.Message)).ToArray();
    }

    private async Task<IReadOnlyList<ElevatedRollbackOperationResult>> ApplyEnvironmentAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var report = await ReadReportAsync<EnvironmentRollbackReport>(request.ReportJsonPath, cancellationToken).ConfigureAwait(false);
        var selectedIds = ParseSelectedOperationIds(request.OperationSelector, report.RollbackPlan.Select(operation => operation.Id));
        var plan = new EnvironmentRollbackPlan(
            Guid.NewGuid(),
            Guid.Empty,
            services.GetRequiredService<IClock>().UtcNow,
            report.RollbackPlan,
            []);
        var results = await services.GetRequiredService<EnvironmentRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, cancellationToken).ConfigureAwait(false);
        return results.Select(result => new ElevatedRollbackOperationResult(result.OperationId, result.Succeeded, result.ValidationState, result.Message)).ToArray();
    }

    private async Task<IReadOnlyList<ElevatedRollbackOperationResult>> ApplyHostsFileAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var report = await ReadReportAsync<HostsFileRollbackReport>(request.ReportJsonPath, cancellationToken).ConfigureAwait(false);
        var selectedIds = ParseSelectedOperationIds(request.OperationSelector, report.RollbackPlan.Select(operation => operation.Id));
        var plan = new HostsFileRollbackPlan(
            Guid.NewGuid(),
            Guid.Empty,
            services.GetRequiredService<IClock>().UtcNow,
            report.RollbackPlan,
            []);
        var results = await services.GetRequiredService<HostsFileRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, cancellationToken).ConfigureAwait(false);
        return results.Select(result => new ElevatedRollbackOperationResult(result.OperationId, result.Succeeded, result.ValidationState, result.Message)).ToArray();
    }

    private async Task<IReadOnlyList<ElevatedRollbackOperationResult>> ApplyFirewallAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var report = await ReadReportAsync<FirewallRollbackReport>(request.ReportJsonPath, cancellationToken).ConfigureAwait(false);
        var selectedIds = ParseSelectedOperationIds(request.OperationSelector, report.RollbackPlan.Select(operation => operation.Id));
        var plan = new FirewallRollbackPlan(
            Guid.NewGuid(),
            Guid.Empty,
            services.GetRequiredService<IClock>().UtcNow,
            report.RollbackPlan,
            []);
        var results = await services.GetRequiredService<FirewallRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, cancellationToken).ConfigureAwait(false);
        return results.Select(result => new ElevatedRollbackOperationResult(result.OperationId, result.Succeeded, result.ValidationState, result.Message)).ToArray();
    }

    private async Task<IReadOnlyList<ElevatedRollbackOperationResult>> ApplyFileSystemAsync(
        ElevatedRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var report = await ReadReportAsync<FileSystemRollbackReport>(request.ReportJsonPath, cancellationToken).ConfigureAwait(false);
        var selectedIds = ParseSelectedOperationIds(request.OperationSelector, report.RollbackPlan.Select(operation => operation.Id));
        var plan = new FileSystemRollbackPlan(
            Guid.NewGuid(),
            Guid.Empty,
            services.GetRequiredService<IClock>().UtcNow,
            report.RollbackPlan,
            []);
        var results = await services.GetRequiredService<FileSystemRollbackExecutor>()
            .ApplyAsync(plan, selectedIds, cancellationToken).ConfigureAwait(false);
        return results.Select(result => new ElevatedRollbackOperationResult(result.OperationId, result.Succeeded, result.ValidationState, result.Message)).ToArray();
    }

    private static async Task<TReport> ReadReportAsync<TReport>(string path, CancellationToken cancellationToken)
    {
        var reportJson = await File.ReadAllTextAsync(Path.GetFullPath(path), cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<TReport>(reportJson, WinLedgerJsonSerializer.Options)
            ?? throw new InvalidOperationException("Rollback report could not be read.");
    }

    private static async Task<ElevatedRollbackRequest> ReadRequestAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(Path.GetFullPath(path), cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ElevatedRollbackRequest>(json, WinLedgerJsonSerializer.Options)
            ?? throw new InvalidOperationException("Elevated helper request could not be read.");
    }

    private static async Task WriteResponseAsync(
        string path,
        ElevatedRollbackResponse response,
        CancellationToken cancellationToken)
    {
        var responsePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(responsePath) ?? ".");
        await File.WriteAllTextAsync(
            responsePath,
            JsonSerializer.Serialize(response, WinLedgerJsonSerializer.Options),
            cancellationToken).ConfigureAwait(false);
    }

    private static HashSet<Guid> ParseSelectedOperationIds(string value, IEnumerable<Guid> allOperationIds)
    {
        return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? allOperationIds.ToHashSet()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .ToHashSet();
    }

    private static ElevatedRollbackResponse Failure(Guid requestId, bool authenticated, string warning)
    {
        return new ElevatedRollbackResponse(requestId, authenticated, false, [], [warning]);
    }

    private static (string RequestPath, string ResponsePath) ParseArgs(string[] args)
    {
        string? requestPath = null;
        string? responsePath = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--request":
                    requestPath = RequireValue(args, ref index, "--request");
                    break;

                case "--response":
                    responsePath = RequireValue(args, ref index, "--response");
                    break;

                default:
                    throw new ArgumentException($"Unknown helper argument: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(requestPath) || string.IsNullOrWhiteSpace(responsePath))
        {
            throw new ArgumentException("Usage: WinLedger.ElevatedHelper --request <request-json> --response <response-json>");
        }

        return (requestPath, responsePath);
    }

    private static string RequireValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{name} requires a path.");
        }

        index++;
        return args[index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            WinLedger elevated helper

            Usage:
              WinLedger.ElevatedHelper --request <request-json> --response <response-json>

            The helper accepts only WinLedger rollback request files and known rollback subsystems.
            """);
    }

    private sealed record RegistryRollbackReport(IReadOnlyList<RegistryRollbackOperation> RollbackPlan);

    private sealed record ServiceRollbackReport(IReadOnlyList<ServiceRollbackOperation> RollbackPlan);

    private sealed record ScheduledTaskRollbackReport(IReadOnlyList<ScheduledTaskRollbackOperation> RollbackPlan);

    private sealed record StartupRollbackReport(IReadOnlyList<StartupRollbackOperation> RollbackPlan);

    private sealed record EnvironmentRollbackReport(IReadOnlyList<EnvironmentRollbackOperation> RollbackPlan);

    private sealed record HostsFileRollbackReport(IReadOnlyList<HostsFileRollbackOperation> RollbackPlan);

    private sealed record FirewallRollbackReport(IReadOnlyList<FirewallRollbackOperation> RollbackPlan);

    private sealed record FileSystemRollbackReport(IReadOnlyList<FileSystemRollbackOperation> RollbackPlan);
}
