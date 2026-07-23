using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WinLedger.App.ViewModels;
using WinLedger.Comparison.EnvironmentVariables;
using WinLedger.Comparison.FileSystem;
using WinLedger.Comparison.Firewall;
using WinLedger.Comparison.Hosts;
using WinLedger.Comparison.InstalledApplications;
using WinLedger.Comparison.Registry;
using WinLedger.Comparison.ScheduledTasks;
using WinLedger.Comparison.Services;
using WinLedger.Comparison.Startup;
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
using WinLedger.Rollback.EnvironmentVariables;
using WinLedger.Rollback.FileSystem;
using WinLedger.Rollback.Firewall;
using WinLedger.Rollback.Hosts;
using WinLedger.Rollback.InstalledApplications;
using WinLedger.Rollback.Registry;
using WinLedger.Rollback.ScheduledTasks;
using WinLedger.Rollback.Services;
using WinLedger.Rollback.Startup;
using WinLedger.Storage.Sqlite;
using WinLedger.Windows.EnvironmentVariables;
using WinLedger.Windows.FileSystem;
using WinLedger.Windows.Firewall;
using WinLedger.Windows.Hosts;
using WinLedger.Windows.InstalledApplications;
using WinLedger.Windows.Registry;
using WinLedger.Windows.ScheduledTasks;
using WinLedger.Windows.Services;
using WinLedger.Windows.Startup;

namespace WinLedger.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinLedger",
            "winledger.db");

        services = new ServiceCollection()
            .AddSingleton<IClock, SystemClock>()
            .AddSingleton<IRegistrySnapshotCollector, WindowsRegistrySnapshotCollector>()
            .AddSingleton<IRegistryMutationProvider, WindowsRegistryMutationProvider>()
            .AddSingleton<IServiceSnapshotCollector, WindowsServiceSnapshotCollector>()
            .AddSingleton<IServiceMutationProvider, WindowsServiceMutationProvider>()
            .AddSingleton<IScheduledTaskSnapshotCollector, WindowsScheduledTaskSnapshotCollector>()
            .AddSingleton<IScheduledTaskMutationProvider, WindowsScheduledTaskMutationProvider>()
            .AddSingleton<IStartupSnapshotCollector, WindowsStartupSnapshotCollector>()
            .AddSingleton<IStartupMutationProvider, WindowsStartupMutationProvider>()
            .AddSingleton<IEnvironmentSnapshotCollector, WindowsEnvironmentSnapshotCollector>()
            .AddSingleton<IEnvironmentMutationProvider, WindowsEnvironmentMutationProvider>()
            .AddSingleton<IHostsFileSnapshotCollector, WindowsHostsFileSnapshotCollector>()
            .AddSingleton<IHostsFileMutationProvider, WindowsHostsFileMutationProvider>()
            .AddSingleton<IFirewallSnapshotCollector, WindowsFirewallSnapshotCollector>()
            .AddSingleton<IFirewallMutationProvider, WindowsFirewallMutationProvider>()
            .AddSingleton<IInstalledApplicationSnapshotCollector, WindowsInstalledApplicationSnapshotCollector>()
            .AddSingleton<IFileSystemSnapshotCollector, WindowsFileSystemSnapshotCollector>()
            .AddSingleton<IFileSystemMutationProvider, WindowsFileSystemMutationProvider>()
            .AddSingleton(_ => new SqliteWinLedgerStore(databasePath))
            .AddSingleton<ITrackingSessionStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IRegistrySnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IServiceSnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IScheduledTaskSnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IStartupSnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IEnvironmentSnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IHostsFileSnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IFirewallSnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IInstalledApplicationSnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<IFileSystemSnapshotStore>(provider => provider.GetRequiredService<SqliteWinLedgerStore>())
            .AddSingleton<RegistrySnapshotComparer>()
            .AddSingleton<ServiceSnapshotComparer>()
            .AddSingleton<ScheduledTaskSnapshotComparer>()
            .AddSingleton<StartupSnapshotComparer>()
            .AddSingleton<EnvironmentSnapshotComparer>()
            .AddSingleton<HostsFileSnapshotComparer>()
            .AddSingleton<FirewallSnapshotComparer>()
            .AddSingleton<InstalledApplicationsSnapshotComparer>()
            .AddSingleton<FileSystemSnapshotComparer>()
            .AddSingleton<RegistryRollbackPlanner>()
            .AddSingleton<ServiceRollbackPlanner>()
            .AddSingleton<ScheduledTaskRollbackPlanner>()
            .AddSingleton<StartupRollbackPlanner>()
            .AddSingleton<EnvironmentRollbackPlanner>()
            .AddSingleton<HostsFileRollbackPlanner>()
            .AddSingleton<FirewallRollbackPlanner>()
            .AddSingleton<InstalledApplicationRollbackPlanner>()
            .AddSingleton<FileSystemRollbackPlanner>()
            .AddSingleton<RegistryRollbackExecutor>()
            .AddSingleton<ServiceRollbackExecutor>()
            .AddSingleton<ScheduledTaskRollbackExecutor>()
            .AddSingleton<StartupRollbackExecutor>()
            .AddSingleton<EnvironmentRollbackExecutor>()
            .AddSingleton<HostsFileRollbackExecutor>()
            .AddSingleton<FirewallRollbackExecutor>()
            .AddSingleton<FileSystemRollbackExecutor>()
            .AddSingleton<RegistryReportExporter>()
            .AddSingleton<ServiceReportExporter>()
            .AddSingleton<ScheduledTaskReportExporter>()
            .AddSingleton<StartupReportExporter>()
            .AddSingleton<EnvironmentReportExporter>()
            .AddSingleton<HostsFileReportExporter>()
            .AddSingleton<FirewallReportExporter>()
            .AddSingleton<InstalledApplicationReportExporter>()
            .AddSingleton<FileSystemReportExporter>()
            .AddSingleton<TrackingSessionCaptureOrchestrator>()
            .AddTransient<MainWindowViewModel>()
            .AddTransient<MainWindow>()
            .BuildServiceProvider();

        services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        services?.Dispose();
        base.OnExit(e);
    }
}
