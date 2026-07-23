using Microsoft.Win32;
using System.ServiceProcess;
using WinLedger.Core.Services;
using WinLedger.Domain.Services;

namespace WinLedger.Windows.Services;

public sealed class WindowsServiceMutationProvider : IServiceMutationProvider
{
    public Task<WindowsServiceSnapshot?> ReadServiceAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var state = ReadState(serviceName);
        return Task.FromResult(ServiceRegistryReader.ReadExistingServiceSnapshot(serviceName, state, null));
    }

    public Task SetStartModeAsync(
        string serviceName,
        ServiceStartModeKind startMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = ServiceRegistryReader.OpenServiceKeyForWriting(serviceName);
        key.SetValue("Start", ServiceRegistryReader.ToWindowsStartValue(startMode), RegistryValueKind.DWord);
        return Task.CompletedTask;
    }

    public Task SetDelayedAutoStartAsync(
        string serviceName,
        bool delayedAutoStart,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = ServiceRegistryReader.OpenServiceKeyForWriting(serviceName);
        key.SetValue("DelayedAutoStart", delayedAutoStart ? 1 : 0, RegistryValueKind.DWord);
        return Task.CompletedTask;
    }

    private static ServiceStateKind ReadState(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
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
            return ServiceStateKind.Unknown;
        }
    }
}
