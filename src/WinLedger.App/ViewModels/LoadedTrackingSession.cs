using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Firewall;
using WinLedger.Domain.Hosts;
using WinLedger.Domain.InstalledApplications;
using WinLedger.Domain.Registry;
using WinLedger.Domain.ScheduledTasks;
using WinLedger.Domain.Services;
using WinLedger.Domain.Sessions;
using WinLedger.Domain.Startup;

namespace WinLedger.App.ViewModels;

public sealed record LoadedTrackingSession(
    TrackingSession Session,
    RegistrySnapshot? RegistryBaselineSnapshot,
    RegistryComparison? RegistryComparison,
    ServiceSnapshot? ServiceBaselineSnapshot,
    ServiceComparison? ServiceComparison,
    ScheduledTaskSnapshot? ScheduledTaskBaselineSnapshot,
    ScheduledTaskComparison? ScheduledTaskComparison,
    StartupSnapshot? StartupBaselineSnapshot,
    StartupComparison? StartupComparison,
    EnvironmentSnapshot? EnvironmentBaselineSnapshot,
    EnvironmentComparison? EnvironmentComparison,
    HostsFileSnapshot? HostsFileBaselineSnapshot,
    HostsFileComparison? HostsFileComparison,
    FirewallSnapshot? FirewallBaselineSnapshot,
    FirewallComparison? FirewallComparison,
    InstalledApplicationsSnapshot? InstalledApplicationsBaselineSnapshot,
    InstalledApplicationsComparison? InstalledApplicationsComparison,
    FileSystemSnapshot? FileSystemBaselineSnapshot,
    FileSystemComparison? FileSystemComparison);
