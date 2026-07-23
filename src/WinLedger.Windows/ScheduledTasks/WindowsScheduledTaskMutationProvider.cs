using Microsoft.CSharp.RuntimeBinder;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Windows.ScheduledTasks;

public sealed class WindowsScheduledTaskMutationProvider : IScheduledTaskMutationProvider
{
    public Task<ScheduledTaskDefinitionSnapshot?> ReadTaskAsync(
        string taskPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var (folderPath, taskName) = ScheduledTaskPath.Split(taskPath);
            dynamic folder = GetFolder(folderPath);
            dynamic task = folder.GetTask(taskName);
            return Task.FromResult<ScheduledTaskDefinitionSnapshot?>(TaskSchedulerComMapper.FromRegisteredTask(task));
        }
        catch (Exception ex) when (IsTaskSchedulerException(ex))
        {
            return Task.FromResult<ScheduledTaskDefinitionSnapshot?>(null);
        }
    }

    public Task DeleteTaskAsync(
        string taskPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (folderPath, taskName) = ScheduledTaskPath.Split(taskPath);
        dynamic folder = GetFolder(folderPath);
        folder.DeleteTask(taskName, 0);
        return Task.CompletedTask;
    }

    public Task SetEnabledAsync(
        string taskPath,
        bool enabled,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (folderPath, taskName) = ScheduledTaskPath.Split(taskPath);
        dynamic folder = GetFolder(folderPath);
        dynamic task = folder.GetTask(taskName);
        task.Enabled = enabled;
        return Task.CompletedTask;
    }

    private static object GetFolder(string folderPath)
    {
        dynamic service = CreateScheduleService();
        service.Connect();
        return service.GetFolder(folderPath);
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
