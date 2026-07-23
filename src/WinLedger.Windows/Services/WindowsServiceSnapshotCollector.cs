using System.ServiceProcess;
using WinLedger.Core.Abstractions;
using WinLedger.Core.Services;
using WinLedger.Domain.Services;

namespace WinLedger.Windows.Services;

public sealed class WindowsServiceSnapshotCollector(IClock clock) : IServiceSnapshotCollector
{
    public Task<ServiceSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var services = new List<WindowsServiceSnapshot>();
        var warnings = new List<string>();

        ServiceController[] controllers;
        try
        {
            controllers = ServiceController.GetServices();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            warnings.Add($"Service collection failed: {ex.Message}");
            return Task.FromResult(CreateSnapshot(sessionId, snapshotName, services, warnings));
        }

        foreach (var controller in controllers.OrderBy(controller => controller.ServiceName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (controller)
            {
                var serviceName = controller.ServiceName;
                var displayName = ReadDisplayName(controller, warnings);
                var state = ReadState(controller, warnings);
                services.Add(ServiceRegistryReader.ReadServiceSnapshot(serviceName, displayName, state, warnings));
            }
        }

        return Task.FromResult(CreateSnapshot(sessionId, snapshotName, services, warnings));
    }

    private ServiceSnapshot CreateSnapshot(
        Guid sessionId,
        string snapshotName,
        IReadOnlyList<WindowsServiceSnapshot> services,
        IReadOnlyList<string> warnings)
    {
        return new ServiceSnapshot(
            Guid.NewGuid(),
            sessionId,
            snapshotName,
            clock.UtcNow,
            services,
            warnings);
    }

    private static string? ReadDisplayName(ServiceController controller, ICollection<string> warnings)
    {
        try
        {
            return controller.DisplayName;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            warnings.Add($"Service display name could not be read for {controller.ServiceName}: {ex.Message}");
            return null;
        }
    }

    private static ServiceStateKind ReadState(ServiceController controller, ICollection<string> warnings)
    {
        try
        {
            return controller.Status switch
            {
                ServiceControllerStatus.Stopped => ServiceStateKind.Stopped,
                ServiceControllerStatus.StartPending => ServiceStateKind.StartPending,
                ServiceControllerStatus.StopPending => ServiceStateKind.StopPending,
                ServiceControllerStatus.Running => ServiceStateKind.Running,
                ServiceControllerStatus.ContinuePending => ServiceStateKind.ContinuePending,
                ServiceControllerStatus.PausePending => ServiceStateKind.PausePending,
                ServiceControllerStatus.Paused => ServiceStateKind.Paused,
                _ => ServiceStateKind.Unknown
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            warnings.Add($"Service state could not be read for {controller.ServiceName}: {ex.Message}");
            return ServiceStateKind.Unknown;
        }
    }
}
