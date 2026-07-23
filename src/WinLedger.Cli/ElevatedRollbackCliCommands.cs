using Microsoft.Extensions.DependencyInjection;
using WinLedger.Core.Elevation;

namespace WinLedger.Cli;

internal sealed class ElevatedRollbackCliCommands(IServiceProvider services)
{
    public async Task<int> ApplyAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: winledger elevated-rollback-apply <subsystem> <report-json> <operation-id|all> [helper-exe] [--no-elevation]");
            return 2;
        }

        var subsystem = ParseSubsystem(args[1]);
        var helperPath = ResolveHelperPath(args);
        var requestElevation = !args.Any(argument => string.Equals(argument, "--no-elevation", StringComparison.OrdinalIgnoreCase));

        var response = await services.GetRequiredService<ElevatedHelperClient>()
            .ApplyRollbackAsync(subsystem, args[2], args[3], helperPath, requestElevation, CancellationToken.None)
            .ConfigureAwait(false);

        foreach (var warning in response.Warnings)
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }

        foreach (var result in response.Results)
        {
            Console.WriteLine($"{result.OperationId}: {result.ValidationState} - {result.Message}");
        }

        if (!response.Authenticated)
        {
            return 5;
        }

        return response.Succeeded ? 0 : 4;
    }

    private static ElevatedRollbackSubsystem ParseSubsystem(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "registry" => ElevatedRollbackSubsystem.Registry,
            "services" or "service" => ElevatedRollbackSubsystem.Services,
            "tasks" or "scheduled-tasks" or "scheduledtasks" => ElevatedRollbackSubsystem.ScheduledTasks,
            "startup" => ElevatedRollbackSubsystem.Startup,
            "environment" or "environment-variables" or "environmentvariables" => ElevatedRollbackSubsystem.Environment,
            "hosts" or "hosts-file" or "hostsfile" => ElevatedRollbackSubsystem.HostsFile,
            "firewall" => ElevatedRollbackSubsystem.Firewall,
            "files" or "file-system" or "filesystem" => ElevatedRollbackSubsystem.FileSystem,
            _ => throw new ArgumentException($"Unsupported rollback subsystem: {value}")
        };
    }

    private static string ResolveHelperPath(string[] args)
    {
        var explicitPath = args.Skip(4)
            .FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "WinLedger.ElevatedHelper.exe"),
            Path.Combine(baseDirectory, "helper", "WinLedger.ElevatedHelper.exe"),
            Path.Combine(baseDirectory, "..", "helper", "WinLedger.ElevatedHelper.exe")
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Elevated helper executable was not found. Provide an explicit helper path.");
    }
}
