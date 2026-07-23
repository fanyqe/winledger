using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Core.ScheduledTasks;

public interface IScheduledTaskMutationProvider
{
    Task<ScheduledTaskDefinitionSnapshot?> ReadTaskAsync(
        string taskPath,
        CancellationToken cancellationToken);

    Task DeleteTaskAsync(
        string taskPath,
        CancellationToken cancellationToken);

    Task SetEnabledAsync(
        string taskPath,
        bool enabled,
        CancellationToken cancellationToken);
}
