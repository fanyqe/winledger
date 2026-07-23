using System.IO;
using WinLedger.Core.Elevation;

namespace WinLedger.App.ViewModels;

public sealed class WpfElevatedRollbackService(ElevatedHelperClient elevatedHelperClient)
{
    public async Task<ElevatedRollbackResponse> ApplyAsync(
        ElevatedRollbackSubsystem subsystem,
        string reportJson,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var reportDirectory = Path.Combine(
            Path.GetTempPath(),
            "WinLedger",
            "RollbackReports",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(reportDirectory);

        var reportPath = Path.Combine(reportDirectory, "report.json");
        await File.WriteAllTextAsync(reportPath, reportJson, cancellationToken).ConfigureAwait(false);

        try
        {
            return await elevatedHelperClient.ApplyRollbackAsync(
                subsystem,
                reportPath,
                operationId.ToString("D"),
                ResolveHelperPath(),
                requestElevation: true,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(reportDirectory);
        }
    }

    private static string ResolveHelperPath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "WinLedger.ElevatedHelper.exe"),
            Path.Combine(baseDirectory, "helper", "WinLedger.ElevatedHelper.exe"),
            Path.Combine(baseDirectory, "..", "helper", "WinLedger.ElevatedHelper.exe")
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Elevated helper executable was not found in the application output.");
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
