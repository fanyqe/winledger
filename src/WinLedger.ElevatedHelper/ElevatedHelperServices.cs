using Microsoft.Extensions.DependencyInjection;
using WinLedger.Core.Abstractions;
using WinLedger.Core.EnvironmentVariables;
using WinLedger.Core.FileSystem;
using WinLedger.Core.Firewall;
using WinLedger.Core.Hosts;
using WinLedger.Core.Registry;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Core.Services;
using WinLedger.Core.Startup;
using WinLedger.Rollback.EnvironmentVariables;
using WinLedger.Rollback.FileSystem;
using WinLedger.Rollback.Firewall;
using WinLedger.Rollback.Hosts;
using WinLedger.Rollback.Registry;
using WinLedger.Rollback.ScheduledTasks;
using WinLedger.Rollback.Services;
using WinLedger.Rollback.Startup;
using WinLedger.Windows.EnvironmentVariables;
using WinLedger.Windows.FileSystem;
using WinLedger.Windows.Firewall;
using WinLedger.Windows.Hosts;
using WinLedger.Windows.Registry;
using WinLedger.Windows.ScheduledTasks;
using WinLedger.Windows.Services;
using WinLedger.Windows.Startup;

namespace WinLedger.ElevatedHelper;

internal static class ElevatedHelperServices
{
    public static ServiceProvider Create()
    {
        return new ServiceCollection()
            .AddSingleton<IClock, SystemClock>()
            .AddSingleton<IRegistryMutationProvider, WindowsRegistryMutationProvider>()
            .AddSingleton<IServiceMutationProvider, WindowsServiceMutationProvider>()
            .AddSingleton<IScheduledTaskMutationProvider, WindowsScheduledTaskMutationProvider>()
            .AddSingleton<IStartupMutationProvider, WindowsStartupMutationProvider>()
            .AddSingleton<IEnvironmentMutationProvider, WindowsEnvironmentMutationProvider>()
            .AddSingleton<IHostsFileMutationProvider, WindowsHostsFileMutationProvider>()
            .AddSingleton<IFirewallMutationProvider, WindowsFirewallMutationProvider>()
            .AddSingleton<IFileSystemMutationProvider, WindowsFileSystemMutationProvider>()
            .AddSingleton<RegistryRollbackExecutor>()
            .AddSingleton<ServiceRollbackExecutor>()
            .AddSingleton<ScheduledTaskRollbackExecutor>()
            .AddSingleton<StartupRollbackExecutor>()
            .AddSingleton<EnvironmentRollbackExecutor>()
            .AddSingleton<HostsFileRollbackExecutor>()
            .AddSingleton<FirewallRollbackExecutor>()
            .AddSingleton<FileSystemRollbackExecutor>()
            .AddSingleton<ElevatedHelperAuditLogger>()
            .BuildServiceProvider();
    }
}
