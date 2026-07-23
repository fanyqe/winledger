using Microsoft.CSharp.RuntimeBinder;
using WinLedger.Core.Abstractions;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Windows.ScheduledTasks;

public sealed class WindowsScheduledTaskSnapshotCollector(IClock clock) : IScheduledTaskSnapshotCollector
{
    public Task<ScheduledTaskSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var tasks = new List<ScheduledTaskDefinitionSnapshot>();
        var warnings = new List<string>();

        try
        {
            dynamic service = CreateScheduleService();
            service.Connect();
            dynamic rootFolder = service.GetFolder("\\");
            CaptureFolder(rootFolder, tasks, warnings, cancellationToken);
        }
        catch (Exception ex) when (IsTaskSchedulerException(ex))
        {
            warnings.Add($"Scheduled task collection failed: {ex.Message}");
        }

        return Task.FromResult(new ScheduledTaskSnapshot(
            Guid.NewGuid(),
            sessionId,
            snapshotName,
            clock.UtcNow,
            tasks.OrderBy(task => task.FullPath, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings));
    }

    private static void CaptureFolder(
        dynamic folder,
        List<ScheduledTaskDefinitionSnapshot> tasks,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string folderPath = folder.Path;

        try
        {
            foreach (dynamic registeredTask in folder.GetTasks(0))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    tasks.Add(TaskSchedulerComMapper.FromRegisteredTask(registeredTask));
                }
                catch (Exception ex) when (IsTaskSchedulerException(ex))
                {
                    warnings.Add($"Scheduled task could not be read in {folderPath}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (IsTaskSchedulerException(ex))
        {
            warnings.Add($"Scheduled tasks could not be enumerated in {folderPath}: {ex.Message}");
        }

        try
        {
            foreach (dynamic childFolder in folder.GetFolders(0))
            {
                CaptureFolder(childFolder, tasks, warnings, cancellationToken);
            }
        }
        catch (Exception ex) when (IsTaskSchedulerException(ex))
        {
            warnings.Add($"Scheduled task folders could not be enumerated in {folderPath}: {ex.Message}");
        }
    }

    private static object CreateScheduleService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Task Scheduler COM service is not available.");

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Task Scheduler COM service could not be created.");
    }

    private static bool IsTaskSchedulerException(Exception ex)
    {
        return ex is InvalidOperationException
            or UnauthorizedAccessException
            or RuntimeBinderException
            or System.Runtime.InteropServices.COMException;
    }
}
