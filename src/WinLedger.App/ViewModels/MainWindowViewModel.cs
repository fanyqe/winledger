using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;
using WinLedger.App.Infrastructure;
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
using WinLedger.Core.Reports;
using WinLedger.Core.Registry;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Core.Services;
using WinLedger.Core.Sessions;
using WinLedger.Core.Startup;
using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Firewall;
using WinLedger.Domain.Hosts;
using WinLedger.Domain.InstalledApplications;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.ScheduledTasks;
using WinLedger.Domain.Services;
using WinLedger.Domain.Sessions;
using WinLedger.Domain.Startup;
using WinLedger.Rollback.EnvironmentVariables;
using WinLedger.Rollback.FileSystem;
using WinLedger.Rollback.Firewall;
using WinLedger.Rollback.Hosts;
using WinLedger.Rollback.InstalledApplications;
using WinLedger.Rollback.Registry;
using WinLedger.Rollback.ScheduledTasks;
using WinLedger.Rollback.Services;
using WinLedger.Rollback.Startup;

namespace WinLedger.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IClock clock;
    private readonly TrackingSessionReopenService sessionReopenService;
    private readonly IRegistrySnapshotCollector registryCollector;
    private readonly IRegistrySnapshotStore registryStore;
    private readonly RegistrySnapshotComparer registryComparer;
    private readonly RegistryRollbackPlanner registryRollbackPlanner;
    private readonly RegistryRollbackExecutor registryRollbackExecutor;
    private readonly RegistryReportExporter registryExporter;
    private readonly IServiceSnapshotCollector serviceCollector;
    private readonly IServiceSnapshotStore serviceStore;
    private readonly ServiceSnapshotComparer serviceComparer;
    private readonly ServiceRollbackPlanner serviceRollbackPlanner;
    private readonly ServiceRollbackExecutor serviceRollbackExecutor;
    private readonly ServiceReportExporter serviceExporter;
    private readonly IScheduledTaskSnapshotCollector scheduledTaskCollector;
    private readonly IScheduledTaskSnapshotStore scheduledTaskStore;
    private readonly ScheduledTaskSnapshotComparer scheduledTaskComparer;
    private readonly ScheduledTaskRollbackPlanner scheduledTaskRollbackPlanner;
    private readonly ScheduledTaskRollbackExecutor scheduledTaskRollbackExecutor;
    private readonly ScheduledTaskReportExporter scheduledTaskExporter;
    private readonly IStartupSnapshotCollector startupCollector;
    private readonly IStartupSnapshotStore startupStore;
    private readonly StartupSnapshotComparer startupComparer;
    private readonly StartupRollbackPlanner startupRollbackPlanner;
    private readonly StartupRollbackExecutor startupRollbackExecutor;
    private readonly StartupReportExporter startupExporter;
    private readonly IEnvironmentSnapshotCollector environmentCollector;
    private readonly IEnvironmentSnapshotStore environmentStore;
    private readonly EnvironmentSnapshotComparer environmentComparer;
    private readonly EnvironmentRollbackPlanner environmentRollbackPlanner;
    private readonly EnvironmentRollbackExecutor environmentRollbackExecutor;
    private readonly EnvironmentReportExporter environmentExporter;
    private readonly IHostsFileSnapshotCollector hostsFileCollector;
    private readonly IHostsFileSnapshotStore hostsFileStore;
    private readonly HostsFileSnapshotComparer hostsFileComparer;
    private readonly HostsFileRollbackPlanner hostsFileRollbackPlanner;
    private readonly HostsFileRollbackExecutor hostsFileRollbackExecutor;
    private readonly HostsFileReportExporter hostsFileExporter;
    private readonly IFirewallSnapshotCollector firewallCollector;
    private readonly IFirewallSnapshotStore firewallStore;
    private readonly FirewallSnapshotComparer firewallComparer;
    private readonly FirewallRollbackPlanner firewallRollbackPlanner;
    private readonly FirewallRollbackExecutor firewallRollbackExecutor;
    private readonly FirewallReportExporter firewallExporter;
    private readonly IInstalledApplicationSnapshotCollector installedApplicationCollector;
    private readonly IInstalledApplicationSnapshotStore installedApplicationStore;
    private readonly InstalledApplicationsSnapshotComparer installedApplicationComparer;
    private readonly InstalledApplicationRollbackPlanner installedApplicationRollbackPlanner;
    private readonly InstalledApplicationReportExporter installedApplicationExporter;
    private readonly IFileSystemSnapshotCollector fileSystemCollector;
    private readonly IFileSystemSnapshotStore fileSystemStore;
    private readonly FileSystemSnapshotComparer fileSystemComparer;
    private readonly FileSystemRollbackPlanner fileSystemRollbackPlanner;
    private readonly FileSystemRollbackExecutor fileSystemRollbackExecutor;
    private readonly FileSystemReportExporter fileSystemExporter;
    private RegistrySnapshot? registryBaselineSnapshot;
    private RegistryComparison? registryComparison;
    private RegistryRollbackPlan? registryRollbackPlan;
    private RegistryRollbackOperation? selectedRegistryRollbackOperation;
    private ServiceSnapshot? serviceBaselineSnapshot;
    private ServiceComparison? serviceComparison;
    private ServiceRollbackPlan? serviceRollbackPlan;
    private ServiceRollbackOperation? selectedServiceRollbackOperation;
    private ScheduledTaskSnapshot? scheduledTaskBaselineSnapshot;
    private ScheduledTaskComparison? scheduledTaskComparison;
    private ScheduledTaskRollbackPlan? scheduledTaskRollbackPlan;
    private ScheduledTaskRollbackOperation? selectedScheduledTaskRollbackOperation;
    private StartupSnapshot? startupBaselineSnapshot;
    private StartupComparison? startupComparison;
    private StartupRollbackPlan? startupRollbackPlan;
    private StartupRollbackOperation? selectedStartupRollbackOperation;
    private EnvironmentSnapshot? environmentBaselineSnapshot;
    private EnvironmentComparison? environmentComparison;
    private EnvironmentRollbackPlan? environmentRollbackPlan;
    private EnvironmentRollbackOperation? selectedEnvironmentRollbackOperation;
    private HostsFileSnapshot? hostsFileBaselineSnapshot;
    private HostsFileComparison? hostsFileComparison;
    private HostsFileRollbackPlan? hostsFileRollbackPlan;
    private HostsFileRollbackOperation? selectedHostsFileRollbackOperation;
    private FirewallSnapshot? firewallBaselineSnapshot;
    private FirewallComparison? firewallComparison;
    private FirewallRollbackPlan? firewallRollbackPlan;
    private FirewallRollbackOperation? selectedFirewallRollbackOperation;
    private InstalledApplicationsSnapshot? installedApplicationBaselineSnapshot;
    private InstalledApplicationsComparison? installedApplicationComparison;
    private InstalledApplicationRollbackPlan? installedApplicationRollbackPlan;
    private FileSystemSnapshot? fileSystemBaselineSnapshot;
    private FileSystemComparison? fileSystemComparison;
    private FileSystemRollbackPlan? fileSystemRollbackPlan;
    private FileSystemRollbackOperation? selectedFileSystemRollbackOperation;
    private TrackingSessionListItem? selectedSession;
    private string status = "Ready";
    private string registrySessionTitle = "Registry tracking session";
    private string serviceSessionTitle = "Services tracking session";
    private string scheduledTaskSessionTitle = "Scheduled tasks tracking session";
    private string startupSessionTitle = "Startup tracking session";
    private string environmentSessionTitle = "Environment tracking session";
    private string hostsFileSessionTitle = "Hosts file tracking session";
    private string firewallSessionTitle = "Firewall tracking session";
    private string installedApplicationsSessionTitle = "Installed applications tracking session";
    private string fileSystemSessionTitle = "File-system tracking session";
    private string fileSystemRootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "FileTrackingSample");
    private string fileSystemBackupSizeLimitText = "262144";
    private bool fileSystemCalculateHashes;
    private bool fileSystemBackupSmallFiles;
    private bool fileSystemIncludeNoise;
    private string registryPath = @"HKCU\Software\WinLedger\TestSandbox";
    private string registryExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "registry-report.json");
    private string serviceExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "services-report.json");
    private string scheduledTaskExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "scheduled-tasks-report.json");
    private string startupExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "startup-report.json");
    private string environmentExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "environment-report.json");
    private string hostsFileExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "hosts-file-report.json");
    private string firewallExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "firewall-report.json");
    private string installedApplicationsExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "installed-applications-report.json");
    private string fileSystemExportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "file-system-report.json");

    public MainWindowViewModel(
        IClock clock,
        TrackingSessionReopenService sessionReopenService,
        IRegistrySnapshotCollector registryCollector,
        IRegistrySnapshotStore registryStore,
        RegistrySnapshotComparer registryComparer,
        RegistryRollbackPlanner registryRollbackPlanner,
        RegistryRollbackExecutor registryRollbackExecutor,
        RegistryReportExporter registryExporter,
        IServiceSnapshotCollector serviceCollector,
        IServiceSnapshotStore serviceStore,
        ServiceSnapshotComparer serviceComparer,
        ServiceRollbackPlanner serviceRollbackPlanner,
        ServiceRollbackExecutor serviceRollbackExecutor,
        ServiceReportExporter serviceExporter,
        IScheduledTaskSnapshotCollector scheduledTaskCollector,
        IScheduledTaskSnapshotStore scheduledTaskStore,
        ScheduledTaskSnapshotComparer scheduledTaskComparer,
        ScheduledTaskRollbackPlanner scheduledTaskRollbackPlanner,
        ScheduledTaskRollbackExecutor scheduledTaskRollbackExecutor,
        ScheduledTaskReportExporter scheduledTaskExporter,
        IStartupSnapshotCollector startupCollector,
        IStartupSnapshotStore startupStore,
        StartupSnapshotComparer startupComparer,
        StartupRollbackPlanner startupRollbackPlanner,
        StartupRollbackExecutor startupRollbackExecutor,
        StartupReportExporter startupExporter,
        IEnvironmentSnapshotCollector environmentCollector,
        IEnvironmentSnapshotStore environmentStore,
        EnvironmentSnapshotComparer environmentComparer,
        EnvironmentRollbackPlanner environmentRollbackPlanner,
        EnvironmentRollbackExecutor environmentRollbackExecutor,
        EnvironmentReportExporter environmentExporter,
        IHostsFileSnapshotCollector hostsFileCollector,
        IHostsFileSnapshotStore hostsFileStore,
        HostsFileSnapshotComparer hostsFileComparer,
        HostsFileRollbackPlanner hostsFileRollbackPlanner,
        HostsFileRollbackExecutor hostsFileRollbackExecutor,
        HostsFileReportExporter hostsFileExporter,
        IFirewallSnapshotCollector firewallCollector,
        IFirewallSnapshotStore firewallStore,
        FirewallSnapshotComparer firewallComparer,
        FirewallRollbackPlanner firewallRollbackPlanner,
        FirewallRollbackExecutor firewallRollbackExecutor,
        FirewallReportExporter firewallExporter,
        IInstalledApplicationSnapshotCollector installedApplicationCollector,
        IInstalledApplicationSnapshotStore installedApplicationStore,
        InstalledApplicationsSnapshotComparer installedApplicationComparer,
        InstalledApplicationRollbackPlanner installedApplicationRollbackPlanner,
        InstalledApplicationReportExporter installedApplicationExporter,
        IFileSystemSnapshotCollector fileSystemCollector,
        IFileSystemSnapshotStore fileSystemStore,
        FileSystemSnapshotComparer fileSystemComparer,
        FileSystemRollbackPlanner fileSystemRollbackPlanner,
        FileSystemRollbackExecutor fileSystemRollbackExecutor,
        FileSystemReportExporter fileSystemExporter)
    {
        this.clock = clock;
        this.sessionReopenService = sessionReopenService;
        this.registryCollector = registryCollector;
        this.registryStore = registryStore;
        this.registryComparer = registryComparer;
        this.registryRollbackPlanner = registryRollbackPlanner;
        this.registryRollbackExecutor = registryRollbackExecutor;
        this.registryExporter = registryExporter;
        this.serviceCollector = serviceCollector;
        this.serviceStore = serviceStore;
        this.serviceComparer = serviceComparer;
        this.serviceRollbackPlanner = serviceRollbackPlanner;
        this.serviceRollbackExecutor = serviceRollbackExecutor;
        this.serviceExporter = serviceExporter;
        this.scheduledTaskCollector = scheduledTaskCollector;
        this.scheduledTaskStore = scheduledTaskStore;
        this.scheduledTaskComparer = scheduledTaskComparer;
        this.scheduledTaskRollbackPlanner = scheduledTaskRollbackPlanner;
        this.scheduledTaskRollbackExecutor = scheduledTaskRollbackExecutor;
        this.scheduledTaskExporter = scheduledTaskExporter;
        this.startupCollector = startupCollector;
        this.startupStore = startupStore;
        this.startupComparer = startupComparer;
        this.startupRollbackPlanner = startupRollbackPlanner;
        this.startupRollbackExecutor = startupRollbackExecutor;
        this.startupExporter = startupExporter;
        this.environmentCollector = environmentCollector;
        this.environmentStore = environmentStore;
        this.environmentComparer = environmentComparer;
        this.environmentRollbackPlanner = environmentRollbackPlanner;
        this.environmentRollbackExecutor = environmentRollbackExecutor;
        this.environmentExporter = environmentExporter;
        this.hostsFileCollector = hostsFileCollector;
        this.hostsFileStore = hostsFileStore;
        this.hostsFileComparer = hostsFileComparer;
        this.hostsFileRollbackPlanner = hostsFileRollbackPlanner;
        this.hostsFileRollbackExecutor = hostsFileRollbackExecutor;
        this.hostsFileExporter = hostsFileExporter;
        this.firewallCollector = firewallCollector;
        this.firewallStore = firewallStore;
        this.firewallComparer = firewallComparer;
        this.firewallRollbackPlanner = firewallRollbackPlanner;
        this.firewallRollbackExecutor = firewallRollbackExecutor;
        this.firewallExporter = firewallExporter;
        this.installedApplicationCollector = installedApplicationCollector;
        this.installedApplicationStore = installedApplicationStore;
        this.installedApplicationComparer = installedApplicationComparer;
        this.installedApplicationRollbackPlanner = installedApplicationRollbackPlanner;
        this.installedApplicationExporter = installedApplicationExporter;
        this.fileSystemCollector = fileSystemCollector;
        this.fileSystemStore = fileSystemStore;
        this.fileSystemComparer = fileSystemComparer;
        this.fileSystemRollbackPlanner = fileSystemRollbackPlanner;
        this.fileSystemRollbackExecutor = fileSystemRollbackExecutor;
        this.fileSystemExporter = fileSystemExporter;

        RefreshSessionsCommand = new AsyncRelayCommand(RefreshSessionsAsync);
        OpenSelectedSessionCommand = new AsyncRelayCommand(OpenSelectedSessionAsync, () => SelectedSession is not null);

        CaptureRegistryBaselineCommand = new AsyncRelayCommand(CaptureRegistryBaselineAsync);
        CaptureRegistryComparisonCommand = new AsyncRelayCommand(CaptureRegistryComparisonAsync, () => registryBaselineSnapshot is not null);
        ExportRegistryJsonCommand = new AsyncRelayCommand(ExportRegistryJsonAsync, () => registryComparison is not null);
        CreateRegistryRollbackPlanCommand = new AsyncRelayCommand(CreateRegistryRollbackPlanAsync, () => registryComparison is not null);
        ExecuteSelectedRegistryRollbackCommand = new AsyncRelayCommand(ExecuteSelectedRegistryRollbackAsync, () => SelectedRegistryRollbackOperation is not null);

        CaptureServiceBaselineCommand = new AsyncRelayCommand(CaptureServiceBaselineAsync);
        CaptureServiceComparisonCommand = new AsyncRelayCommand(CaptureServiceComparisonAsync, () => serviceBaselineSnapshot is not null);
        ExportServiceJsonCommand = new AsyncRelayCommand(ExportServiceJsonAsync, () => serviceComparison is not null);
        CreateServiceRollbackPlanCommand = new AsyncRelayCommand(CreateServiceRollbackPlanAsync, () => serviceComparison is not null);
        ExecuteSelectedServiceRollbackCommand = new AsyncRelayCommand(ExecuteSelectedServiceRollbackAsync, () => SelectedServiceRollbackOperation is not null);

        CaptureScheduledTaskBaselineCommand = new AsyncRelayCommand(CaptureScheduledTaskBaselineAsync);
        CaptureScheduledTaskComparisonCommand = new AsyncRelayCommand(CaptureScheduledTaskComparisonAsync, () => scheduledTaskBaselineSnapshot is not null);
        ExportScheduledTaskJsonCommand = new AsyncRelayCommand(ExportScheduledTaskJsonAsync, () => scheduledTaskComparison is not null);
        CreateScheduledTaskRollbackPlanCommand = new AsyncRelayCommand(CreateScheduledTaskRollbackPlanAsync, () => scheduledTaskComparison is not null);
        ExecuteSelectedScheduledTaskRollbackCommand = new AsyncRelayCommand(ExecuteSelectedScheduledTaskRollbackAsync, () => SelectedScheduledTaskRollbackOperation is not null);

        CaptureStartupBaselineCommand = new AsyncRelayCommand(CaptureStartupBaselineAsync);
        CaptureStartupComparisonCommand = new AsyncRelayCommand(CaptureStartupComparisonAsync, () => startupBaselineSnapshot is not null);
        ExportStartupJsonCommand = new AsyncRelayCommand(ExportStartupJsonAsync, () => startupComparison is not null);
        CreateStartupRollbackPlanCommand = new AsyncRelayCommand(CreateStartupRollbackPlanAsync, () => startupComparison is not null);
        ExecuteSelectedStartupRollbackCommand = new AsyncRelayCommand(ExecuteSelectedStartupRollbackAsync, () => SelectedStartupRollbackOperation is not null);

        CaptureEnvironmentBaselineCommand = new AsyncRelayCommand(CaptureEnvironmentBaselineAsync);
        CaptureEnvironmentComparisonCommand = new AsyncRelayCommand(CaptureEnvironmentComparisonAsync, () => environmentBaselineSnapshot is not null);
        ExportEnvironmentJsonCommand = new AsyncRelayCommand(ExportEnvironmentJsonAsync, () => environmentComparison is not null);
        CreateEnvironmentRollbackPlanCommand = new AsyncRelayCommand(CreateEnvironmentRollbackPlanAsync, () => environmentComparison is not null);
        ExecuteSelectedEnvironmentRollbackCommand = new AsyncRelayCommand(ExecuteSelectedEnvironmentRollbackAsync, () => SelectedEnvironmentRollbackOperation is not null);

        CaptureHostsFileBaselineCommand = new AsyncRelayCommand(CaptureHostsFileBaselineAsync);
        CaptureHostsFileComparisonCommand = new AsyncRelayCommand(CaptureHostsFileComparisonAsync, () => hostsFileBaselineSnapshot is not null);
        ExportHostsFileJsonCommand = new AsyncRelayCommand(ExportHostsFileJsonAsync, () => hostsFileComparison is not null);
        CreateHostsFileRollbackPlanCommand = new AsyncRelayCommand(CreateHostsFileRollbackPlanAsync, () => hostsFileComparison is not null);
        ExecuteSelectedHostsFileRollbackCommand = new AsyncRelayCommand(ExecuteSelectedHostsFileRollbackAsync, () => SelectedHostsFileRollbackOperation is not null);

        CaptureFirewallBaselineCommand = new AsyncRelayCommand(CaptureFirewallBaselineAsync);
        CaptureFirewallComparisonCommand = new AsyncRelayCommand(CaptureFirewallComparisonAsync, () => firewallBaselineSnapshot is not null);
        ExportFirewallJsonCommand = new AsyncRelayCommand(ExportFirewallJsonAsync, () => firewallComparison is not null);
        CreateFirewallRollbackPlanCommand = new AsyncRelayCommand(CreateFirewallRollbackPlanAsync, () => firewallComparison is not null);
        ExecuteSelectedFirewallRollbackCommand = new AsyncRelayCommand(ExecuteSelectedFirewallRollbackAsync, () => SelectedFirewallRollbackOperation is not null);

        CaptureInstalledApplicationsBaselineCommand = new AsyncRelayCommand(CaptureInstalledApplicationsBaselineAsync);
        CaptureInstalledApplicationsComparisonCommand = new AsyncRelayCommand(CaptureInstalledApplicationsComparisonAsync, () => installedApplicationBaselineSnapshot is not null);
        ExportInstalledApplicationsJsonCommand = new AsyncRelayCommand(ExportInstalledApplicationsJsonAsync, () => installedApplicationComparison is not null);
        CreateInstalledApplicationsRollbackPlanCommand = new AsyncRelayCommand(CreateInstalledApplicationsRollbackPlanAsync, () => installedApplicationComparison is not null);

        CaptureFileSystemBaselineCommand = new AsyncRelayCommand(CaptureFileSystemBaselineAsync);
        CaptureFileSystemComparisonCommand = new AsyncRelayCommand(CaptureFileSystemComparisonAsync, () => fileSystemBaselineSnapshot is not null);
        ExportFileSystemJsonCommand = new AsyncRelayCommand(ExportFileSystemJsonAsync, () => fileSystemComparison is not null);
        CreateFileSystemRollbackPlanCommand = new AsyncRelayCommand(CreateFileSystemRollbackPlanAsync, () => fileSystemComparison is not null);
        ExecuteSelectedFileSystemRollbackCommand = new AsyncRelayCommand(ExecuteSelectedFileSystemRollbackAsync, () => SelectedFileSystemRollbackOperation is not null);

        _ = RefreshSessionsOnStartupAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TrackingSessionListItem> SessionHistory { get; } = [];

    public ObservableCollection<RegistryChange> RegistryChanges { get; } = [];

    public ObservableCollection<RegistryRollbackOperation> RegistryRollbackOperations { get; } = [];

    public ObservableCollection<ServiceChange> ServiceChanges { get; } = [];

    public ObservableCollection<ServiceRollbackOperation> ServiceRollbackOperations { get; } = [];

    public ObservableCollection<ScheduledTaskChange> ScheduledTaskChanges { get; } = [];

    public ObservableCollection<ScheduledTaskRollbackOperation> ScheduledTaskRollbackOperations { get; } = [];

    public ObservableCollection<StartupChange> StartupChanges { get; } = [];

    public ObservableCollection<StartupRollbackOperation> StartupRollbackOperations { get; } = [];

    public ObservableCollection<EnvironmentVariableChange> EnvironmentChanges { get; } = [];

    public ObservableCollection<EnvironmentRollbackOperation> EnvironmentRollbackOperations { get; } = [];

    public ObservableCollection<HostsFileChange> HostsFileChanges { get; } = [];

    public ObservableCollection<HostsFileRollbackOperation> HostsFileRollbackOperations { get; } = [];

    public ObservableCollection<FirewallRuleChange> FirewallChanges { get; } = [];

    public ObservableCollection<FirewallRollbackOperation> FirewallRollbackOperations { get; } = [];

    public ObservableCollection<InstalledApplicationChange> InstalledApplicationChanges { get; } = [];

    public ObservableCollection<string> InstalledApplicationRollbackWarnings { get; } = [];

    public ObservableCollection<FileSystemChange> FileSystemChanges { get; } = [];

    public ObservableCollection<FileSystemRollbackOperation> FileSystemRollbackOperations { get; } = [];

    public ICommand RefreshSessionsCommand { get; }

    public ICommand OpenSelectedSessionCommand { get; }

    public ICommand CaptureRegistryBaselineCommand { get; }

    public ICommand CaptureRegistryComparisonCommand { get; }

    public ICommand ExportRegistryJsonCommand { get; }

    public ICommand CreateRegistryRollbackPlanCommand { get; }

    public ICommand ExecuteSelectedRegistryRollbackCommand { get; }

    public ICommand CaptureServiceBaselineCommand { get; }

    public ICommand CaptureServiceComparisonCommand { get; }

    public ICommand ExportServiceJsonCommand { get; }

    public ICommand CreateServiceRollbackPlanCommand { get; }

    public ICommand ExecuteSelectedServiceRollbackCommand { get; }

    public ICommand CaptureScheduledTaskBaselineCommand { get; }

    public ICommand CaptureScheduledTaskComparisonCommand { get; }

    public ICommand ExportScheduledTaskJsonCommand { get; }

    public ICommand CreateScheduledTaskRollbackPlanCommand { get; }

    public ICommand ExecuteSelectedScheduledTaskRollbackCommand { get; }

    public ICommand CaptureStartupBaselineCommand { get; }

    public ICommand CaptureStartupComparisonCommand { get; }

    public ICommand ExportStartupJsonCommand { get; }

    public ICommand CreateStartupRollbackPlanCommand { get; }

    public ICommand ExecuteSelectedStartupRollbackCommand { get; }

    public ICommand CaptureEnvironmentBaselineCommand { get; }

    public ICommand CaptureEnvironmentComparisonCommand { get; }

    public ICommand ExportEnvironmentJsonCommand { get; }

    public ICommand CreateEnvironmentRollbackPlanCommand { get; }

    public ICommand ExecuteSelectedEnvironmentRollbackCommand { get; }

    public ICommand CaptureHostsFileBaselineCommand { get; }

    public ICommand CaptureHostsFileComparisonCommand { get; }

    public ICommand ExportHostsFileJsonCommand { get; }

    public ICommand CreateHostsFileRollbackPlanCommand { get; }

    public ICommand ExecuteSelectedHostsFileRollbackCommand { get; }

    public ICommand CaptureFirewallBaselineCommand { get; }

    public ICommand CaptureFirewallComparisonCommand { get; }

    public ICommand ExportFirewallJsonCommand { get; }

    public ICommand CreateFirewallRollbackPlanCommand { get; }

    public ICommand ExecuteSelectedFirewallRollbackCommand { get; }

    public ICommand CaptureInstalledApplicationsBaselineCommand { get; }

    public ICommand CaptureInstalledApplicationsComparisonCommand { get; }

    public ICommand ExportInstalledApplicationsJsonCommand { get; }

    public ICommand CreateInstalledApplicationsRollbackPlanCommand { get; }

    public ICommand CaptureFileSystemBaselineCommand { get; }

    public ICommand CaptureFileSystemComparisonCommand { get; }

    public ICommand ExportFileSystemJsonCommand { get; }

    public ICommand CreateFileSystemRollbackPlanCommand { get; }

    public ICommand ExecuteSelectedFileSystemRollbackCommand { get; }

    public string RegistrySessionTitle
    {
        get => registrySessionTitle;
        set => SetProperty(ref registrySessionTitle, value);
    }

    public string ServiceSessionTitle
    {
        get => serviceSessionTitle;
        set => SetProperty(ref serviceSessionTitle, value);
    }

    public string ScheduledTaskSessionTitle
    {
        get => scheduledTaskSessionTitle;
        set => SetProperty(ref scheduledTaskSessionTitle, value);
    }

    public string StartupSessionTitle
    {
        get => startupSessionTitle;
        set => SetProperty(ref startupSessionTitle, value);
    }

    public string EnvironmentSessionTitle
    {
        get => environmentSessionTitle;
        set => SetProperty(ref environmentSessionTitle, value);
    }

    public string HostsFileSessionTitle
    {
        get => hostsFileSessionTitle;
        set => SetProperty(ref hostsFileSessionTitle, value);
    }

    public string FirewallSessionTitle
    {
        get => firewallSessionTitle;
        set => SetProperty(ref firewallSessionTitle, value);
    }

    public string InstalledApplicationsSessionTitle
    {
        get => installedApplicationsSessionTitle;
        set => SetProperty(ref installedApplicationsSessionTitle, value);
    }

    public string FileSystemSessionTitle
    {
        get => fileSystemSessionTitle;
        set => SetProperty(ref fileSystemSessionTitle, value);
    }

    public string FileSystemRootPath
    {
        get => fileSystemRootPath;
        set => SetProperty(ref fileSystemRootPath, value);
    }

    public bool FileSystemCalculateHashes
    {
        get => fileSystemCalculateHashes;
        set => SetProperty(ref fileSystemCalculateHashes, value);
    }

    public bool FileSystemBackupSmallFiles
    {
        get => fileSystemBackupSmallFiles;
        set => SetProperty(ref fileSystemBackupSmallFiles, value);
    }

    public bool FileSystemIncludeNoise
    {
        get => fileSystemIncludeNoise;
        set => SetProperty(ref fileSystemIncludeNoise, value);
    }

    public string FileSystemBackupSizeLimitText
    {
        get => fileSystemBackupSizeLimitText;
        set => SetProperty(ref fileSystemBackupSizeLimitText, value);
    }

    public string RegistryPath
    {
        get => registryPath;
        set => SetProperty(ref registryPath, value);
    }

    public string RegistryExportPath
    {
        get => registryExportPath;
        set => SetProperty(ref registryExportPath, value);
    }

    public string ServiceExportPath
    {
        get => serviceExportPath;
        set => SetProperty(ref serviceExportPath, value);
    }

    public string ScheduledTaskExportPath
    {
        get => scheduledTaskExportPath;
        set => SetProperty(ref scheduledTaskExportPath, value);
    }

    public string StartupExportPath
    {
        get => startupExportPath;
        set => SetProperty(ref startupExportPath, value);
    }

    public string EnvironmentExportPath
    {
        get => environmentExportPath;
        set => SetProperty(ref environmentExportPath, value);
    }

    public string HostsFileExportPath
    {
        get => hostsFileExportPath;
        set => SetProperty(ref hostsFileExportPath, value);
    }

    public string FirewallExportPath
    {
        get => firewallExportPath;
        set => SetProperty(ref firewallExportPath, value);
    }

    public string InstalledApplicationsExportPath
    {
        get => installedApplicationsExportPath;
        set => SetProperty(ref installedApplicationsExportPath, value);
    }

    public string FileSystemExportPath
    {
        get => fileSystemExportPath;
        set => SetProperty(ref fileSystemExportPath, value);
    }

    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    public TrackingSessionListItem? SelectedSession
    {
        get => selectedSession;
        set
        {
            SetProperty(ref selectedSession, value);
            (OpenSelectedSessionCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public RegistryRollbackOperation? SelectedRegistryRollbackOperation
    {
        get => selectedRegistryRollbackOperation;
        set
        {
            SetProperty(ref selectedRegistryRollbackOperation, value);
            (ExecuteSelectedRegistryRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ServiceRollbackOperation? SelectedServiceRollbackOperation
    {
        get => selectedServiceRollbackOperation;
        set
        {
            SetProperty(ref selectedServiceRollbackOperation, value);
            (ExecuteSelectedServiceRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ScheduledTaskRollbackOperation? SelectedScheduledTaskRollbackOperation
    {
        get => selectedScheduledTaskRollbackOperation;
        set
        {
            SetProperty(ref selectedScheduledTaskRollbackOperation, value);
            (ExecuteSelectedScheduledTaskRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public StartupRollbackOperation? SelectedStartupRollbackOperation
    {
        get => selectedStartupRollbackOperation;
        set
        {
            SetProperty(ref selectedStartupRollbackOperation, value);
            (ExecuteSelectedStartupRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public EnvironmentRollbackOperation? SelectedEnvironmentRollbackOperation
    {
        get => selectedEnvironmentRollbackOperation;
        set
        {
            SetProperty(ref selectedEnvironmentRollbackOperation, value);
            (ExecuteSelectedEnvironmentRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public HostsFileRollbackOperation? SelectedHostsFileRollbackOperation
    {
        get => selectedHostsFileRollbackOperation;
        set
        {
            SetProperty(ref selectedHostsFileRollbackOperation, value);
            (ExecuteSelectedHostsFileRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public FirewallRollbackOperation? SelectedFirewallRollbackOperation
    {
        get => selectedFirewallRollbackOperation;
        set
        {
            SetProperty(ref selectedFirewallRollbackOperation, value);
            (ExecuteSelectedFirewallRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public FileSystemRollbackOperation? SelectedFileSystemRollbackOperation
    {
        get => selectedFileSystemRollbackOperation;
        set
        {
            SetProperty(ref selectedFileSystemRollbackOperation, value);
            (ExecuteSelectedFileSystemRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private async Task RefreshSessionsOnStartupAsync()
    {
        try
        {
            await RefreshSessionHistoryAsync(null, updateStatus: false).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Status = $"Session history could not be loaded: {ex.Message}";
        }
    }

    private async Task RefreshSessionsAsync()
    {
        await RefreshSessionHistoryAsync(SelectedSession?.Id, updateStatus: true).ConfigureAwait(true);
    }

    private async Task RefreshSessionHistoryAsync(Guid? selectedSessionId, bool updateStatus)
    {
        var sessions = await sessionReopenService.ListSessionsAsync(CancellationToken.None).ConfigureAwait(true);
        SessionHistory.Clear();
        foreach (var session in sessions)
        {
            SessionHistory.Add(session);
        }

        SelectedSession = selectedSessionId.HasValue
            ? SessionHistory.FirstOrDefault(session => session.Id == selectedSessionId.Value)
            : SessionHistory.FirstOrDefault();

        if (updateStatus)
        {
            Status = $"Session history loaded: {SessionHistory.Count} sessions.";
        }
    }

    private async Task OpenSelectedSessionAsync()
    {
        if (SelectedSession is null)
        {
            Status = "Select a session first.";
            return;
        }

        var loadedSession = await sessionReopenService.LoadAsync(SelectedSession.Id, CancellationToken.None)
            .ConfigureAwait(true);
        ApplyLoadedSession(loadedSession);
    }

    private void ApplyLoadedSession(LoadedTrackingSession loadedSession)
    {
        ApplySessionTitle(loadedSession.Session.Title);

        registryBaselineSnapshot = loadedSession.RegistryBaselineSnapshot;
        serviceBaselineSnapshot = loadedSession.ServiceBaselineSnapshot;
        scheduledTaskBaselineSnapshot = loadedSession.ScheduledTaskBaselineSnapshot;
        startupBaselineSnapshot = loadedSession.StartupBaselineSnapshot;
        environmentBaselineSnapshot = loadedSession.EnvironmentBaselineSnapshot;
        hostsFileBaselineSnapshot = loadedSession.HostsFileBaselineSnapshot;
        firewallBaselineSnapshot = loadedSession.FirewallBaselineSnapshot;
        installedApplicationBaselineSnapshot = loadedSession.InstalledApplicationsBaselineSnapshot;
        fileSystemBaselineSnapshot = loadedSession.FileSystemBaselineSnapshot;

        registryComparison = loadedSession.RegistryComparison;
        serviceComparison = loadedSession.ServiceComparison;
        scheduledTaskComparison = loadedSession.ScheduledTaskComparison;
        startupComparison = loadedSession.StartupComparison;
        environmentComparison = loadedSession.EnvironmentComparison;
        hostsFileComparison = loadedSession.HostsFileComparison;
        firewallComparison = loadedSession.FirewallComparison;
        installedApplicationComparison = loadedSession.InstalledApplicationsComparison;
        fileSystemComparison = loadedSession.FileSystemComparison;

        registryRollbackPlan = null;
        serviceRollbackPlan = null;
        scheduledTaskRollbackPlan = null;
        startupRollbackPlan = null;
        environmentRollbackPlan = null;
        hostsFileRollbackPlan = null;
        firewallRollbackPlan = null;
        installedApplicationRollbackPlan = null;
        fileSystemRollbackPlan = null;

        SelectedRegistryRollbackOperation = null;
        SelectedServiceRollbackOperation = null;
        SelectedScheduledTaskRollbackOperation = null;
        SelectedStartupRollbackOperation = null;
        SelectedEnvironmentRollbackOperation = null;
        SelectedHostsFileRollbackOperation = null;
        SelectedFirewallRollbackOperation = null;
        SelectedFileSystemRollbackOperation = null;

        ReplaceCollection(RegistryChanges, registryComparison?.Changes);
        ReplaceCollection(ServiceChanges, serviceComparison?.Changes);
        ReplaceCollection(ScheduledTaskChanges, scheduledTaskComparison?.Changes);
        ReplaceCollection(StartupChanges, startupComparison?.Changes);
        ReplaceCollection(EnvironmentChanges, environmentComparison?.Changes);
        ReplaceCollection(HostsFileChanges, hostsFileComparison?.Changes);
        ReplaceCollection(FirewallChanges, firewallComparison?.Changes);
        ReplaceCollection(InstalledApplicationChanges, installedApplicationComparison?.Changes);
        ReplaceCollection(FileSystemChanges, fileSystemComparison?.Changes);

        RegistryRollbackOperations.Clear();
        ServiceRollbackOperations.Clear();
        ScheduledTaskRollbackOperations.Clear();
        StartupRollbackOperations.Clear();
        EnvironmentRollbackOperations.Clear();
        HostsFileRollbackOperations.Clear();
        FirewallRollbackOperations.Clear();
        InstalledApplicationRollbackWarnings.Clear();
        FileSystemRollbackOperations.Clear();

        ApplyLoadedTargets(loadedSession);

        Status = $"Session opened: {loadedSession.Session.Title}.";
        RefreshCommands();
    }

    private void ApplySessionTitle(string title)
    {
        RegistrySessionTitle = title;
        ServiceSessionTitle = title;
        ScheduledTaskSessionTitle = title;
        StartupSessionTitle = title;
        EnvironmentSessionTitle = title;
        HostsFileSessionTitle = title;
        FirewallSessionTitle = title;
        InstalledApplicationsSessionTitle = title;
        FileSystemSessionTitle = title;
    }

    private void ApplyLoadedTargets(LoadedTrackingSession loadedSession)
    {
        var registryTarget = loadedSession.RegistryBaselineSnapshot?.Targets.FirstOrDefault();
        if (registryTarget is not null)
        {
            RegistryPath = registryTarget.Path.ToString();
        }

        var fileSystemOptions = loadedSession.FileSystemBaselineSnapshot?.Options;
        if (fileSystemOptions is not null)
        {
            FileSystemRootPath = fileSystemOptions.MonitoredRoots.FirstOrDefault() ?? FileSystemRootPath;
            FileSystemCalculateHashes = fileSystemOptions.CalculateHashes;
            FileSystemBackupSmallFiles = fileSystemOptions.BackupSmallFiles;
            FileSystemIncludeNoise = fileSystemOptions.IncludeHighNoise;
            FileSystemBackupSizeLimitText = fileSystemOptions.BackupSizeLimitBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T>? values)
    {
        collection.Clear();
        if (values is null)
        {
            return;
        }

        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private async Task CaptureRegistryBaselineAsync()
    {
        await registryStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(RegistrySessionTitle) ? "Registry tracking session" : RegistrySessionTitle);
        await registryStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        registryBaselineSnapshot = await registryCollector.CaptureAsync(
            session.Id,
            "Baseline",
            [new RegistrySnapshotTarget(WinLedger.Domain.Registry.RegistryPath.Parse(RegistryPath), true)],
            CancellationToken.None).ConfigureAwait(true);
        await registryStore.SaveRegistrySnapshotAsync(registryBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(registryStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        registryComparison = null;
        registryRollbackPlan = null;
        RegistryChanges.Clear();
        RegistryRollbackOperations.Clear();
        Status = $"Baseline captured: {registryBaselineSnapshot.Keys.Count} registry keys.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureRegistryComparisonAsync()
    {
        if (registryBaselineSnapshot is null)
        {
            Status = "Capture a registry baseline first.";
            return;
        }

        var comparisonSnapshot = await registryCollector.CaptureAsync(
            registryBaselineSnapshot.SessionId,
            "Comparison",
            registryBaselineSnapshot.Targets,
            CancellationToken.None).ConfigureAwait(true);
        await registryStore.SaveRegistrySnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(registryStore, registryBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        registryComparison = registryComparer.Compare(registryBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        RegistryChanges.Clear();
        foreach (var change in registryComparison.Changes)
        {
            RegistryChanges.Add(change);
        }

        Status = $"Comparison complete: {registryComparison.Changes.Count} registry changes.";
        await RefreshSessionHistoryAsync(registryBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportRegistryJsonAsync()
    {
        if (registryComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            RegistryExportPath,
            () => registryExporter.ExportJson(registryComparison, registryRollbackPlan),
            () => registryExporter.ExportHtml(registryComparison, registryRollbackPlan),
            () => registryExporter.ExportText(registryComparison, registryRollbackPlan),
            () => registryExporter.ExportReg(registryComparison, registryRollbackPlan),
            () => registryExporter.ExportPowerShell(registryComparison, registryRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(RegistryExportPath, registryEditorSupported: true, powerShellSupported: true)} report exported: {RegistryExportPath}";
    }

    private Task CreateRegistryRollbackPlanAsync()
    {
        if (registryComparison is null)
        {
            return Task.CompletedTask;
        }

        registryRollbackPlan = registryRollbackPlanner.CreatePlan(registryComparison, clock.UtcNow);
        RegistryRollbackOperations.Clear();
        foreach (var operation in registryRollbackPlan.Operations)
        {
            RegistryRollbackOperations.Add(operation);
        }

        Status = $"Rollback plan ready: {registryRollbackPlan.Operations.Count} registry operations.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task ExecuteSelectedRegistryRollbackAsync()
    {
        if (registryRollbackPlan is null || SelectedRegistryRollbackOperation is null)
        {
            Status = "Select a registry rollback operation first.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply rollback operation to {SelectedRegistryRollbackOperation.TargetDisplayName}?",
            "WinLedger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            Status = "Rollback cancelled.";
            return;
        }

        var results = await registryRollbackExecutor.ApplyAsync(
            registryRollbackPlan,
            new HashSet<Guid> { SelectedRegistryRollbackOperation.Id },
            CancellationToken.None).ConfigureAwait(true);
        var result = results.Single();

        Status = result.Succeeded
            ? $"Rollback completed: {SelectedRegistryRollbackOperation.TargetDisplayName}"
            : $"Rollback blocked: {result.Message}";
    }

    private async Task CaptureServiceBaselineAsync()
    {
        await serviceStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(ServiceSessionTitle) ? "Services tracking session" : ServiceSessionTitle);
        await serviceStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        serviceBaselineSnapshot = await serviceCollector.CaptureAsync(
            session.Id,
            "Baseline",
            CancellationToken.None).ConfigureAwait(true);
        await serviceStore.SaveServiceSnapshotAsync(serviceBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(serviceStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        serviceComparison = null;
        serviceRollbackPlan = null;
        ServiceChanges.Clear();
        ServiceRollbackOperations.Clear();
        Status = $"Baseline captured: {serviceBaselineSnapshot.Services.Count} services.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureServiceComparisonAsync()
    {
        if (serviceBaselineSnapshot is null)
        {
            Status = "Capture a services baseline first.";
            return;
        }

        var comparisonSnapshot = await serviceCollector.CaptureAsync(
            serviceBaselineSnapshot.SessionId,
            "Comparison",
            CancellationToken.None).ConfigureAwait(true);
        await serviceStore.SaveServiceSnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(serviceStore, serviceBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        serviceComparison = serviceComparer.Compare(serviceBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        ServiceChanges.Clear();
        foreach (var change in serviceComparison.Changes)
        {
            ServiceChanges.Add(change);
        }

        Status = $"Comparison complete: {serviceComparison.Changes.Count} service changes.";
        await RefreshSessionHistoryAsync(serviceBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportServiceJsonAsync()
    {
        if (serviceComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            ServiceExportPath,
            () => serviceExporter.ExportJson(serviceComparison, serviceRollbackPlan),
            () => serviceExporter.ExportHtml(serviceComparison, serviceRollbackPlan),
            () => serviceExporter.ExportText(serviceComparison, serviceRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(ServiceExportPath)} report exported: {ServiceExportPath}";
    }

    private Task CreateServiceRollbackPlanAsync()
    {
        if (serviceComparison is null)
        {
            return Task.CompletedTask;
        }

        serviceRollbackPlan = serviceRollbackPlanner.CreatePlan(serviceComparison, clock.UtcNow);
        ServiceRollbackOperations.Clear();
        foreach (var operation in serviceRollbackPlan.Operations)
        {
            ServiceRollbackOperations.Add(operation);
        }

        Status = $"Rollback plan ready: {serviceRollbackPlan.Operations.Count} service operations.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task ExecuteSelectedServiceRollbackAsync()
    {
        if (serviceRollbackPlan is null || SelectedServiceRollbackOperation is null)
        {
            Status = "Select a service rollback operation first.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply rollback operation to {SelectedServiceRollbackOperation.TargetDisplayName}?",
            "WinLedger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            Status = "Rollback cancelled.";
            return;
        }

        var results = await serviceRollbackExecutor.ApplyAsync(
            serviceRollbackPlan,
            new HashSet<Guid> { SelectedServiceRollbackOperation.Id },
            CancellationToken.None).ConfigureAwait(true);
        var result = results.Single();

        Status = result.Succeeded
            ? $"Rollback completed: {SelectedServiceRollbackOperation.TargetDisplayName}"
            : $"Rollback blocked: {result.Message}";
    }

    private async Task CaptureScheduledTaskBaselineAsync()
    {
        await scheduledTaskStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(ScheduledTaskSessionTitle) ? "Scheduled tasks tracking session" : ScheduledTaskSessionTitle);
        await scheduledTaskStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        scheduledTaskBaselineSnapshot = await scheduledTaskCollector.CaptureAsync(
            session.Id,
            "Baseline",
            CancellationToken.None).ConfigureAwait(true);
        await scheduledTaskStore.SaveScheduledTaskSnapshotAsync(scheduledTaskBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(scheduledTaskStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        scheduledTaskComparison = null;
        scheduledTaskRollbackPlan = null;
        ScheduledTaskChanges.Clear();
        ScheduledTaskRollbackOperations.Clear();
        Status = $"Baseline captured: {scheduledTaskBaselineSnapshot.Tasks.Count} scheduled tasks.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureScheduledTaskComparisonAsync()
    {
        if (scheduledTaskBaselineSnapshot is null)
        {
            Status = "Capture a scheduled tasks baseline first.";
            return;
        }

        var comparisonSnapshot = await scheduledTaskCollector.CaptureAsync(
            scheduledTaskBaselineSnapshot.SessionId,
            "Comparison",
            CancellationToken.None).ConfigureAwait(true);
        await scheduledTaskStore.SaveScheduledTaskSnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(scheduledTaskStore, scheduledTaskBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        scheduledTaskComparison = scheduledTaskComparer.Compare(scheduledTaskBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        ScheduledTaskChanges.Clear();
        foreach (var change in scheduledTaskComparison.Changes)
        {
            ScheduledTaskChanges.Add(change);
        }

        Status = $"Comparison complete: {scheduledTaskComparison.Changes.Count} scheduled task changes.";
        await RefreshSessionHistoryAsync(scheduledTaskBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportScheduledTaskJsonAsync()
    {
        if (scheduledTaskComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            ScheduledTaskExportPath,
            () => scheduledTaskExporter.ExportJson(scheduledTaskComparison, scheduledTaskRollbackPlan),
            () => scheduledTaskExporter.ExportHtml(scheduledTaskComparison, scheduledTaskRollbackPlan),
            () => scheduledTaskExporter.ExportText(scheduledTaskComparison, scheduledTaskRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(ScheduledTaskExportPath)} report exported: {ScheduledTaskExportPath}";
    }

    private Task CreateScheduledTaskRollbackPlanAsync()
    {
        if (scheduledTaskComparison is null)
        {
            return Task.CompletedTask;
        }

        scheduledTaskRollbackPlan = scheduledTaskRollbackPlanner.CreatePlan(scheduledTaskComparison, clock.UtcNow);
        ScheduledTaskRollbackOperations.Clear();
        foreach (var operation in scheduledTaskRollbackPlan.Operations)
        {
            ScheduledTaskRollbackOperations.Add(operation);
        }

        Status = $"Rollback plan ready: {scheduledTaskRollbackPlan.Operations.Count} scheduled task operations.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task ExecuteSelectedScheduledTaskRollbackAsync()
    {
        if (scheduledTaskRollbackPlan is null || SelectedScheduledTaskRollbackOperation is null)
        {
            Status = "Select a scheduled task rollback operation first.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply rollback operation to {SelectedScheduledTaskRollbackOperation.TargetDisplayName}?",
            "WinLedger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            Status = "Rollback cancelled.";
            return;
        }

        var results = await scheduledTaskRollbackExecutor.ApplyAsync(
            scheduledTaskRollbackPlan,
            new HashSet<Guid> { SelectedScheduledTaskRollbackOperation.Id },
            CancellationToken.None).ConfigureAwait(true);
        var result = results.Single();

        Status = result.Succeeded
            ? $"Rollback completed: {SelectedScheduledTaskRollbackOperation.TargetDisplayName}"
            : $"Rollback blocked: {result.Message}";
    }

    private async Task CaptureStartupBaselineAsync()
    {
        await startupStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(StartupSessionTitle) ? "Startup tracking session" : StartupSessionTitle);
        await startupStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        startupBaselineSnapshot = await startupCollector.CaptureAsync(
            session.Id,
            "Baseline",
            CancellationToken.None).ConfigureAwait(true);
        await startupStore.SaveStartupSnapshotAsync(startupBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(startupStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        startupComparison = null;
        startupRollbackPlan = null;
        StartupChanges.Clear();
        StartupRollbackOperations.Clear();
        Status = $"Baseline captured: {startupBaselineSnapshot.Entries.Count} startup entries.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureStartupComparisonAsync()
    {
        if (startupBaselineSnapshot is null)
        {
            Status = "Capture a startup baseline first.";
            return;
        }

        var comparisonSnapshot = await startupCollector.CaptureAsync(
            startupBaselineSnapshot.SessionId,
            "Comparison",
            CancellationToken.None).ConfigureAwait(true);
        await startupStore.SaveStartupSnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(startupStore, startupBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        startupComparison = startupComparer.Compare(startupBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        StartupChanges.Clear();
        foreach (var change in startupComparison.Changes)
        {
            StartupChanges.Add(change);
        }

        Status = $"Comparison complete: {startupComparison.Changes.Count} startup changes.";
        await RefreshSessionHistoryAsync(startupBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportStartupJsonAsync()
    {
        if (startupComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            StartupExportPath,
            () => startupExporter.ExportJson(startupComparison, startupRollbackPlan),
            () => startupExporter.ExportHtml(startupComparison, startupRollbackPlan),
            () => startupExporter.ExportText(startupComparison, startupRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(StartupExportPath)} report exported: {StartupExportPath}";
    }

    private Task CreateStartupRollbackPlanAsync()
    {
        if (startupComparison is null)
        {
            return Task.CompletedTask;
        }

        startupRollbackPlan = startupRollbackPlanner.CreatePlan(startupComparison, clock.UtcNow);
        StartupRollbackOperations.Clear();
        foreach (var operation in startupRollbackPlan.Operations)
        {
            StartupRollbackOperations.Add(operation);
        }

        Status = $"Rollback plan ready: {startupRollbackPlan.Operations.Count} startup operations.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task ExecuteSelectedStartupRollbackAsync()
    {
        if (startupRollbackPlan is null || SelectedStartupRollbackOperation is null)
        {
            Status = "Select a startup rollback operation first.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply rollback operation to {SelectedStartupRollbackOperation.TargetDisplayName}?",
            "WinLedger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            Status = "Rollback cancelled.";
            return;
        }

        var results = await startupRollbackExecutor.ApplyAsync(
            startupRollbackPlan,
            new HashSet<Guid> { SelectedStartupRollbackOperation.Id },
            CancellationToken.None).ConfigureAwait(true);
        var result = results.Single();

        Status = result.Succeeded
            ? $"Rollback completed: {SelectedStartupRollbackOperation.TargetDisplayName}"
            : $"Rollback blocked: {result.Message}";
    }

    private async Task CaptureEnvironmentBaselineAsync()
    {
        await environmentStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(EnvironmentSessionTitle) ? "Environment tracking session" : EnvironmentSessionTitle);
        await environmentStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        environmentBaselineSnapshot = await environmentCollector.CaptureAsync(
            session.Id,
            "Baseline",
            CancellationToken.None).ConfigureAwait(true);
        await environmentStore.SaveEnvironmentSnapshotAsync(environmentBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(environmentStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        environmentComparison = null;
        environmentRollbackPlan = null;
        EnvironmentChanges.Clear();
        EnvironmentRollbackOperations.Clear();
        Status = $"Baseline captured: {environmentBaselineSnapshot.Variables.Count} environment variables.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureEnvironmentComparisonAsync()
    {
        if (environmentBaselineSnapshot is null)
        {
            Status = "Capture an environment baseline first.";
            return;
        }

        var comparisonSnapshot = await environmentCollector.CaptureAsync(
            environmentBaselineSnapshot.SessionId,
            "Comparison",
            CancellationToken.None).ConfigureAwait(true);
        await environmentStore.SaveEnvironmentSnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(environmentStore, environmentBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        environmentComparison = environmentComparer.Compare(environmentBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        EnvironmentChanges.Clear();
        foreach (var change in environmentComparison.Changes)
        {
            EnvironmentChanges.Add(change);
        }

        Status = $"Comparison complete: {environmentComparison.Changes.Count} environment changes.";
        await RefreshSessionHistoryAsync(environmentBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportEnvironmentJsonAsync()
    {
        if (environmentComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            EnvironmentExportPath,
            () => environmentExporter.ExportJson(environmentComparison, environmentRollbackPlan),
            () => environmentExporter.ExportHtml(environmentComparison, environmentRollbackPlan),
            () => environmentExporter.ExportText(environmentComparison, environmentRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(EnvironmentExportPath)} report exported: {EnvironmentExportPath}";
    }

    private Task CreateEnvironmentRollbackPlanAsync()
    {
        if (environmentComparison is null)
        {
            return Task.CompletedTask;
        }

        environmentRollbackPlan = environmentRollbackPlanner.CreatePlan(environmentComparison, clock.UtcNow);
        EnvironmentRollbackOperations.Clear();
        foreach (var operation in environmentRollbackPlan.Operations)
        {
            EnvironmentRollbackOperations.Add(operation);
        }

        Status = $"Rollback plan ready: {environmentRollbackPlan.Operations.Count} environment operations.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task ExecuteSelectedEnvironmentRollbackAsync()
    {
        if (environmentRollbackPlan is null || SelectedEnvironmentRollbackOperation is null)
        {
            Status = "Select an environment rollback operation first.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply rollback operation to {SelectedEnvironmentRollbackOperation.TargetDisplayName}?",
            "WinLedger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            Status = "Rollback cancelled.";
            return;
        }

        var results = await environmentRollbackExecutor.ApplyAsync(
            environmentRollbackPlan,
            new HashSet<Guid> { SelectedEnvironmentRollbackOperation.Id },
            CancellationToken.None).ConfigureAwait(true);
        var result = results.Single();

        Status = result.Succeeded
            ? $"Rollback completed: {SelectedEnvironmentRollbackOperation.TargetDisplayName}"
            : $"Rollback blocked: {result.Message}";
    }

    private async Task CaptureHostsFileBaselineAsync()
    {
        await hostsFileStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(HostsFileSessionTitle) ? "Hosts file tracking session" : HostsFileSessionTitle);
        await hostsFileStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        hostsFileBaselineSnapshot = await hostsFileCollector.CaptureAsync(
            session.Id,
            "Baseline",
            CancellationToken.None).ConfigureAwait(true);
        await hostsFileStore.SaveHostsFileSnapshotAsync(hostsFileBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(hostsFileStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        hostsFileComparison = null;
        hostsFileRollbackPlan = null;
        HostsFileChanges.Clear();
        HostsFileRollbackOperations.Clear();
        Status = hostsFileBaselineSnapshot.Exists
            ? $"Baseline captured: {hostsFileBaselineSnapshot.Lines.Count} hosts file lines."
            : $"Baseline captured: hosts file missing. Warnings: {hostsFileBaselineSnapshot.Warnings.Count}.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureHostsFileComparisonAsync()
    {
        if (hostsFileBaselineSnapshot is null)
        {
            Status = "Capture a hosts file baseline first.";
            return;
        }

        var comparisonSnapshot = await hostsFileCollector.CaptureAsync(
            hostsFileBaselineSnapshot.SessionId,
            "Comparison",
            CancellationToken.None).ConfigureAwait(true);
        await hostsFileStore.SaveHostsFileSnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(hostsFileStore, hostsFileBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        hostsFileComparison = hostsFileComparer.Compare(hostsFileBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        HostsFileChanges.Clear();
        foreach (var change in hostsFileComparison.Changes)
        {
            HostsFileChanges.Add(change);
        }

        Status = $"Comparison complete: {hostsFileComparison.Changes.Count} hosts file changes.";
        await RefreshSessionHistoryAsync(hostsFileBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportHostsFileJsonAsync()
    {
        if (hostsFileComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            HostsFileExportPath,
            () => hostsFileExporter.ExportJson(hostsFileComparison, hostsFileRollbackPlan),
            () => hostsFileExporter.ExportHtml(hostsFileComparison, hostsFileRollbackPlan),
            () => hostsFileExporter.ExportText(hostsFileComparison, hostsFileRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(HostsFileExportPath)} report exported: {HostsFileExportPath}";
    }

    private Task CreateHostsFileRollbackPlanAsync()
    {
        if (hostsFileComparison is null)
        {
            return Task.CompletedTask;
        }

        hostsFileRollbackPlan = hostsFileRollbackPlanner.CreatePlan(hostsFileComparison, clock.UtcNow);
        HostsFileRollbackOperations.Clear();
        foreach (var operation in hostsFileRollbackPlan.Operations)
        {
            HostsFileRollbackOperations.Add(operation);
        }

        Status = $"Rollback plan ready: {hostsFileRollbackPlan.Operations.Count} hosts file operations.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task ExecuteSelectedHostsFileRollbackAsync()
    {
        if (hostsFileRollbackPlan is null || SelectedHostsFileRollbackOperation is null)
        {
            Status = "Select a hosts file rollback operation first.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply rollback operation to {SelectedHostsFileRollbackOperation.TargetDisplayName}?",
            "WinLedger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            Status = "Rollback cancelled.";
            return;
        }

        var results = await hostsFileRollbackExecutor.ApplyAsync(
            hostsFileRollbackPlan,
            new HashSet<Guid> { SelectedHostsFileRollbackOperation.Id },
            CancellationToken.None).ConfigureAwait(true);
        var result = results.Single();

        Status = result.Succeeded
            ? $"Rollback completed: {SelectedHostsFileRollbackOperation.TargetDisplayName}"
            : $"Rollback blocked: {result.Message}";
    }

    private async Task CaptureFirewallBaselineAsync()
    {
        await firewallStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(FirewallSessionTitle) ? "Firewall tracking session" : FirewallSessionTitle);
        await firewallStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        firewallBaselineSnapshot = await firewallCollector.CaptureAsync(
            session.Id,
            "Baseline",
            CancellationToken.None).ConfigureAwait(true);
        await firewallStore.SaveFirewallSnapshotAsync(firewallBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(firewallStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        firewallComparison = null;
        firewallRollbackPlan = null;
        FirewallChanges.Clear();
        FirewallRollbackOperations.Clear();
        Status = $"Baseline captured: {firewallBaselineSnapshot.Rules.Count} firewall rules. Warnings: {firewallBaselineSnapshot.Warnings.Count}.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureFirewallComparisonAsync()
    {
        if (firewallBaselineSnapshot is null)
        {
            Status = "Capture a firewall baseline first.";
            return;
        }

        var comparisonSnapshot = await firewallCollector.CaptureAsync(
            firewallBaselineSnapshot.SessionId,
            "Comparison",
            CancellationToken.None).ConfigureAwait(true);
        await firewallStore.SaveFirewallSnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(firewallStore, firewallBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        firewallComparison = firewallComparer.Compare(firewallBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        FirewallChanges.Clear();
        foreach (var change in firewallComparison.Changes)
        {
            FirewallChanges.Add(change);
        }

        Status = $"Comparison complete: {firewallComparison.Changes.Count} firewall changes.";
        await RefreshSessionHistoryAsync(firewallBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportFirewallJsonAsync()
    {
        if (firewallComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            FirewallExportPath,
            () => firewallExporter.ExportJson(firewallComparison, firewallRollbackPlan),
            () => firewallExporter.ExportHtml(firewallComparison, firewallRollbackPlan),
            () => firewallExporter.ExportText(firewallComparison, firewallRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(FirewallExportPath)} report exported: {FirewallExportPath}";
    }

    private Task CreateFirewallRollbackPlanAsync()
    {
        if (firewallComparison is null)
        {
            return Task.CompletedTask;
        }

        firewallRollbackPlan = firewallRollbackPlanner.CreatePlan(firewallComparison, clock.UtcNow);
        FirewallRollbackOperations.Clear();
        foreach (var operation in firewallRollbackPlan.Operations)
        {
            FirewallRollbackOperations.Add(operation);
        }

        Status = $"Rollback plan ready: {firewallRollbackPlan.Operations.Count} firewall operations.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task ExecuteSelectedFirewallRollbackAsync()
    {
        if (firewallRollbackPlan is null || SelectedFirewallRollbackOperation is null)
        {
            Status = "Select a firewall rollback operation first.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply rollback operation to {SelectedFirewallRollbackOperation.TargetDisplayName}?",
            "WinLedger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            Status = "Rollback cancelled.";
            return;
        }

        var results = await firewallRollbackExecutor.ApplyAsync(
            firewallRollbackPlan,
            new HashSet<Guid> { SelectedFirewallRollbackOperation.Id },
            CancellationToken.None).ConfigureAwait(true);
        var result = results.Single();

        Status = result.Succeeded
            ? $"Rollback completed: {SelectedFirewallRollbackOperation.TargetDisplayName}"
            : $"Rollback blocked: {result.Message}";
    }

    private async Task CaptureInstalledApplicationsBaselineAsync()
    {
        await installedApplicationStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(InstalledApplicationsSessionTitle) ? "Installed applications tracking session" : InstalledApplicationsSessionTitle);
        await installedApplicationStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        installedApplicationBaselineSnapshot = await installedApplicationCollector.CaptureAsync(
            session.Id,
            "Baseline",
            CancellationToken.None).ConfigureAwait(true);
        await installedApplicationStore.SaveInstalledApplicationsSnapshotAsync(installedApplicationBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(installedApplicationStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        installedApplicationComparison = null;
        installedApplicationRollbackPlan = null;
        InstalledApplicationChanges.Clear();
        InstalledApplicationRollbackWarnings.Clear();
        Status = $"Baseline captured: {installedApplicationBaselineSnapshot.Applications.Count} installed applications. Warnings: {installedApplicationBaselineSnapshot.Warnings.Count}.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureInstalledApplicationsComparisonAsync()
    {
        if (installedApplicationBaselineSnapshot is null)
        {
            Status = "Capture an installed applications baseline first.";
            return;
        }

        var comparisonSnapshot = await installedApplicationCollector.CaptureAsync(
            installedApplicationBaselineSnapshot.SessionId,
            "Comparison",
            CancellationToken.None).ConfigureAwait(true);
        await installedApplicationStore.SaveInstalledApplicationsSnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(installedApplicationStore, installedApplicationBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        installedApplicationComparison = installedApplicationComparer.Compare(installedApplicationBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        InstalledApplicationChanges.Clear();
        foreach (var change in installedApplicationComparison.Changes)
        {
            InstalledApplicationChanges.Add(change);
        }

        Status = $"Comparison complete: {installedApplicationComparison.Changes.Count} installed application changes.";
        await RefreshSessionHistoryAsync(installedApplicationBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportInstalledApplicationsJsonAsync()
    {
        if (installedApplicationComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            InstalledApplicationsExportPath,
            () => installedApplicationExporter.ExportJson(installedApplicationComparison, installedApplicationRollbackPlan),
            () => installedApplicationExporter.ExportHtml(installedApplicationComparison, installedApplicationRollbackPlan),
            () => installedApplicationExporter.ExportText(installedApplicationComparison, installedApplicationRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(InstalledApplicationsExportPath)} report exported: {InstalledApplicationsExportPath}";
    }

    private Task CreateInstalledApplicationsRollbackPlanAsync()
    {
        if (installedApplicationComparison is null)
        {
            return Task.CompletedTask;
        }

        installedApplicationRollbackPlan = installedApplicationRollbackPlanner.CreatePlan(installedApplicationComparison, clock.UtcNow);
        InstalledApplicationRollbackWarnings.Clear();
        foreach (var warning in installedApplicationRollbackPlan.Warnings)
        {
            InstalledApplicationRollbackWarnings.Add(warning);
        }

        Status = $"Manual review plan ready: {installedApplicationRollbackPlan.Warnings.Count} installed application warnings.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task CaptureFileSystemBaselineAsync()
    {
        await fileSystemStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(FileSystemSessionTitle) ? "File-system tracking session" : FileSystemSessionTitle);
        await fileSystemStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        fileSystemBaselineSnapshot = await fileSystemCollector.CaptureAsync(
            session.Id,
            "Baseline",
            CreateFileSystemOptions(),
            CancellationToken.None).ConfigureAwait(true);
        await fileSystemStore.SaveFileSystemSnapshotAsync(fileSystemBaselineSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(fileSystemStore, session.Id, TrackingSessionStatus.BaselineCaptured).ConfigureAwait(true);

        fileSystemComparison = null;
        fileSystemRollbackPlan = null;
        FileSystemChanges.Clear();
        FileSystemRollbackOperations.Clear();
        Status = $"Baseline captured: {fileSystemBaselineSnapshot.Entries.Count} file-system entries. Warnings: {fileSystemBaselineSnapshot.Warnings.Count}.";
        await RefreshSessionHistoryAsync(session.Id, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task CaptureFileSystemComparisonAsync()
    {
        if (fileSystemBaselineSnapshot is null)
        {
            Status = "Capture a file-system baseline first.";
            return;
        }

        var comparisonSnapshot = await fileSystemCollector.CaptureAsync(
            fileSystemBaselineSnapshot.SessionId,
            "Comparison",
            fileSystemBaselineSnapshot.Options,
            CancellationToken.None).ConfigureAwait(true);
        await fileSystemStore.SaveFileSystemSnapshotAsync(comparisonSnapshot, CancellationToken.None).ConfigureAwait(true);
        await UpdateSessionStatusAsync(fileSystemStore, fileSystemBaselineSnapshot.SessionId, TrackingSessionStatus.ComparisonCaptured).ConfigureAwait(true);

        fileSystemComparison = fileSystemComparer.Compare(fileSystemBaselineSnapshot, comparisonSnapshot, clock.UtcNow);
        FileSystemChanges.Clear();
        foreach (var change in fileSystemComparison.Changes)
        {
            FileSystemChanges.Add(change);
        }

        Status = $"Comparison complete: {fileSystemComparison.Changes.Count} file-system changes.";
        await RefreshSessionHistoryAsync(fileSystemBaselineSnapshot.SessionId, updateStatus: false).ConfigureAwait(true);
        RefreshCommands();
    }

    private async Task ExportFileSystemJsonAsync()
    {
        if (fileSystemComparison is null)
        {
            return;
        }

        await WriteReportAsync(
            FileSystemExportPath,
            () => fileSystemExporter.ExportJson(fileSystemComparison, fileSystemRollbackPlan),
            () => fileSystemExporter.ExportHtml(fileSystemComparison, fileSystemRollbackPlan),
            () => fileSystemExporter.ExportText(fileSystemComparison, fileSystemRollbackPlan)).ConfigureAwait(true);
        Status = $"{ReportOutputSelector.FormatName(FileSystemExportPath)} report exported: {FileSystemExportPath}";
    }

    private Task CreateFileSystemRollbackPlanAsync()
    {
        if (fileSystemComparison is null)
        {
            return Task.CompletedTask;
        }

        fileSystemRollbackPlan = fileSystemRollbackPlanner.CreatePlan(fileSystemComparison, clock.UtcNow);
        FileSystemRollbackOperations.Clear();
        foreach (var operation in fileSystemRollbackPlan.Operations)
        {
            FileSystemRollbackOperations.Add(operation);
        }

        Status = $"Rollback plan ready: {fileSystemRollbackPlan.Operations.Count} file-system operations. Warnings: {fileSystemRollbackPlan.Warnings.Count}.";
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task ExecuteSelectedFileSystemRollbackAsync()
    {
        if (fileSystemRollbackPlan is null || SelectedFileSystemRollbackOperation is null)
        {
            Status = "Select a file-system rollback operation first.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply rollback operation to {SelectedFileSystemRollbackOperation.TargetDisplayName}?",
            "WinLedger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            Status = "Rollback cancelled.";
            return;
        }

        var results = await fileSystemRollbackExecutor.ApplyAsync(
            fileSystemRollbackPlan,
            new HashSet<Guid> { SelectedFileSystemRollbackOperation.Id },
            CancellationToken.None).ConfigureAwait(true);
        var result = results.Single();

        Status = result.Succeeded
            ? $"Rollback completed: {SelectedFileSystemRollbackOperation.TargetDisplayName}"
            : $"Rollback blocked: {result.Message}";
    }

    private FileSystemSnapshotOptions CreateFileSystemOptions()
    {
        if (!long.TryParse(FileSystemBackupSizeLimitText, out var backupSizeLimitBytes))
        {
            throw new InvalidOperationException("File backup size limit must be a byte count.");
        }

        return new FileSystemSnapshotOptions(
            [FileSystemRootPath],
            FileSystemSnapshotOptions.DefaultExclusionPatterns,
            FileSystemIncludeNoise,
            FileSystemCalculateHashes,
            FileSystemBackupSmallFiles,
            backupSizeLimitBytes);
    }

    private static async Task WriteReportAsync(
        string path,
        Func<string> jsonExporter,
        Func<string> htmlExporter,
        Func<string> textExporter,
        Func<string>? registryEditorExporter = null,
        Func<string>? powerShellExporter = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        var registryEditorSupported = registryEditorExporter is not null;
        var report = ReportOutputSelector.CreateReport(path, jsonExporter, htmlExporter, textExporter, registryEditorExporter, powerShellExporter);
        await File.WriteAllTextAsync(
            path,
            report,
            ReportOutputSelector.GetEncoding(path, registryEditorSupported)).ConfigureAwait(true);
    }

    private void RefreshCommands()
    {
        (CaptureRegistryComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportRegistryJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateRegistryRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExecuteSelectedRegistryRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CaptureServiceComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportServiceJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateServiceRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExecuteSelectedServiceRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CaptureScheduledTaskComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportScheduledTaskJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateScheduledTaskRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExecuteSelectedScheduledTaskRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CaptureStartupComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportStartupJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateStartupRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExecuteSelectedStartupRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CaptureEnvironmentComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportEnvironmentJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateEnvironmentRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExecuteSelectedEnvironmentRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CaptureHostsFileComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportHostsFileJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateHostsFileRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExecuteSelectedHostsFileRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CaptureFirewallComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportFirewallJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateFirewallRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExecuteSelectedFirewallRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CaptureInstalledApplicationsComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportInstalledApplicationsJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateInstalledApplicationsRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CaptureFileSystemComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportFileSystemJsonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateFileSystemRollbackPlanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExecuteSelectedFileSystemRollbackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private TrackingSession CreateSession(string title)
    {
        return new TrackingSession(
            Guid.NewGuid(),
            title,
            null,
            clock.UtcNow,
            Environment.OSVersion.VersionString,
            RuntimeInformation.ProcessArchitecture.ToString(),
            HashUserSid(),
            IsAdministrator(),
            TrackingSessionStatus.Created);
    }

    private static async Task UpdateSessionStatusAsync(
        ITrackingSessionStore store,
        Guid sessionId,
        TrackingSessionStatus status)
    {
        var session = await store.GetSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(true);
        if (session is null)
        {
            return;
        }

        await store.SaveSessionAsync(session with { Status = status }, CancellationToken.None).ConfigureAwait(true);
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

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
