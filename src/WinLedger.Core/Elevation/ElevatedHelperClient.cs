using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using WinLedger.Domain;

namespace WinLedger.Core.Elevation;

public sealed class ElevatedHelperClient
{
    public async Task<ElevatedRollbackResponse> ApplyRollbackAsync(
        ElevatedRollbackSubsystem subsystem,
        string reportJsonPath,
        string operationSelector,
        string helperExecutablePath,
        bool requestElevation,
        CancellationToken cancellationToken)
    {
        var helperPath = Path.GetFullPath(helperExecutablePath);
        if (!File.Exists(helperPath))
        {
            throw new FileNotFoundException("Elevated helper executable was not found.", helperPath);
        }

        var reportPath = Path.GetFullPath(reportJsonPath);
        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException("Rollback report was not found.", reportPath);
        }

        if (string.IsNullOrWhiteSpace(operationSelector))
        {
            throw new ArgumentException("Operation selector is required.", nameof(operationSelector));
        }

        var requestId = Guid.NewGuid();
        var token = ElevatedHelperAuthenticator.GenerateToken();
        var requestDirectory = Path.Combine(Path.GetTempPath(), "WinLedger", "Elevation", requestId.ToString("N"));
        Directory.CreateDirectory(requestDirectory);

        var requestPath = Path.Combine(requestDirectory, "request.json");
        var responsePath = Path.Combine(requestDirectory, "response.json");
        var request = new ElevatedRollbackRequest(
            ElevatedHelperProtocol.Version,
            requestId,
            subsystem,
            reportPath,
            operationSelector,
            ElevatedHelperAuthenticator.HashToken(token),
            DateTimeOffset.UtcNow);

        await File.WriteAllTextAsync(
            requestPath,
            JsonSerializer.Serialize(request, WinLedgerJsonSerializer.Options),
            cancellationToken).ConfigureAwait(false);

        var previousProcessToken = Environment.GetEnvironmentVariable(ElevatedHelperProtocol.AuthenticationTokenEnvironmentVariable);
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = helperPath,
                UseShellExecute = requestElevation,
                CreateNoWindow = !requestElevation,
                WorkingDirectory = Path.GetDirectoryName(helperPath) ?? Environment.CurrentDirectory
            };

            if (requestElevation)
            {
                startInfo.Verb = "runas";
                Environment.SetEnvironmentVariable(
                    ElevatedHelperProtocol.AuthenticationTokenEnvironmentVariable,
                    token,
                    EnvironmentVariableTarget.Process);
            }
            else
            {
                startInfo.Environment[ElevatedHelperProtocol.AuthenticationTokenEnvironmentVariable] = token;
            }

            startInfo.ArgumentList.Add("--request");
            startInfo.ArgumentList.Add(requestPath);
            startInfo.ArgumentList.Add("--response");
            startInfo.ArgumentList.Add(responsePath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Elevated helper process could not be started.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (!File.Exists(responsePath))
            {
                throw new InvalidOperationException($"Elevated helper exited with code {process.ExitCode} before writing a response.");
            }

            var responseJson = await File.ReadAllTextAsync(responsePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ElevatedRollbackResponse>(responseJson, WinLedgerJsonSerializer.Options)
                ?? throw new InvalidOperationException("Elevated helper response could not be read.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Elevated helper launch was cancelled or blocked.", ex);
        }
        finally
        {
            if (requestElevation)
            {
                Environment.SetEnvironmentVariable(
                    ElevatedHelperProtocol.AuthenticationTokenEnvironmentVariable,
                    previousProcessToken,
                    EnvironmentVariableTarget.Process);
            }
        }
    }
}
