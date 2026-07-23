using Microsoft.Win32;
using WinLedger.Core.Abstractions;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Core.Services;
using WinLedger.Core.Startup;
using WinLedger.Domain.ScheduledTasks;
using WinLedger.Domain.Services;
using WinLedger.Domain.Startup;

namespace WinLedger.Windows.Startup;

public sealed class WindowsStartupSnapshotCollector(
    IClock clock,
    IServiceSnapshotCollector serviceCollector,
    IScheduledTaskSnapshotCollector scheduledTaskCollector) : IStartupSnapshotCollector
{
    public async Task<StartupSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var entries = new List<StartupEntrySnapshot>();
        var warnings = new List<string>();

        CaptureRegistryRunEntries(entries, warnings, cancellationToken);
        CaptureStartupFolderEntries(entries, warnings, cancellationToken);
        await CaptureScheduledTaskEntriesAsync(sessionId, entries, warnings, cancellationToken).ConfigureAwait(false);
        await CaptureServiceEntriesAsync(sessionId, entries, warnings, cancellationToken).ConfigureAwait(false);

        return new StartupSnapshot(
            Guid.NewGuid(),
            sessionId,
            snapshotName,
            clock.UtcNow,
            entries.OrderBy(entry => entry.StableId, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void CaptureRegistryRunEntries(
        List<StartupEntrySnapshot> entries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var targets = new[]
        {
            new RegistryStartupTarget(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupEntrySourceKind.RegistryRun, "HKCU"),
            new RegistryStartupTarget(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupEntrySourceKind.RegistryRunOnce, "HKCU"),
            new RegistryStartupTarget(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupEntrySourceKind.RegistryRun, "HKLM64"),
            new RegistryStartupTarget(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupEntrySourceKind.RegistryRunOnce, "HKLM64"),
            new RegistryStartupTarget(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupEntrySourceKind.RegistryRun, "HKLM32"),
            new RegistryStartupTarget(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupEntrySourceKind.RegistryRunOnce, "HKLM32")
        };

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var root = RegistryKey.OpenBaseKey(target.Hive, target.View);
                using var key = root.OpenSubKey(target.KeyPath, writable: false);
                if (key is null)
                {
                    continue;
                }

                foreach (var valueName in key.GetValueNames().Order(StringComparer.OrdinalIgnoreCase))
                {
                    var command = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
                    entries.Add(new StartupEntrySnapshot(
                        $"{target.DisplayRoot}\\{target.KeyPath}|{valueName}",
                        target.Source,
                        string.IsNullOrEmpty(valueName) ? "(Default)" : valueName,
                        $"{target.DisplayRoot}\\{target.KeyPath}\\{(string.IsNullOrEmpty(valueName) ? "(Default)" : valueName)}",
                        command,
                        true,
                        null,
                        target.Source == StartupEntrySourceKind.RegistryRunOnce ? "Run once at user sign-in" : "Run at user sign-in",
                        "Registry",
                        null,
                        null));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                warnings.Add($"Startup registry entries could not be read at {target.DisplayRoot}\\{target.KeyPath}: {ex.Message}");
            }
        }
    }

    private static void CaptureStartupFolderEntries(
        List<StartupEntrySnapshot> entries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
        };

        foreach (var folder in folders.Where(folder => !string.IsNullOrWhiteSpace(folder)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var filePath in Directory.EnumerateFiles(folder).Order(StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = StartupFolderEntryReader.ReadFile(filePath);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                warnings.Add($"Startup folder entries could not be read at {folder}: {ex.Message}");
            }
        }
    }

    private async Task CaptureScheduledTaskEntriesAsync(
        Guid sessionId,
        List<StartupEntrySnapshot> entries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var snapshot = await scheduledTaskCollector.CaptureAsync(sessionId, "Startup scheduled task source", cancellationToken)
            .ConfigureAwait(false);
        warnings.AddRange(snapshot.Warnings);

        foreach (var task in snapshot.Tasks.Where(IsStartupTask))
        {
            entries.Add(new StartupEntrySnapshot(
                $"ScheduledTask|{task.FullPath}",
                StartupEntrySourceKind.ScheduledTask,
                task.Name,
                task.FullPath,
                task.Actions.FirstOrDefault(action => action.Kind == ScheduledTaskActionKind.Execute)?.Details,
                task.Enabled,
                task.RunAsUser,
                string.Join("; ", task.Triggers.Where(IsStartupTrigger).Select(trigger => trigger.Details)),
                "ScheduledTasks",
                null,
                null));
        }
    }

    private async Task CaptureServiceEntriesAsync(
        Guid sessionId,
        List<StartupEntrySnapshot> entries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var snapshot = await serviceCollector.CaptureAsync(sessionId, "Startup service source", cancellationToken)
            .ConfigureAwait(false);
        warnings.AddRange(snapshot.Warnings);

        foreach (var service in snapshot.Services.Where(IsStartupService))
        {
            entries.Add(new StartupEntrySnapshot(
                $"WindowsService|{service.Name}",
                StartupEntrySourceKind.WindowsService,
                service.DisplayName,
                service.Name,
                service.ExecutablePath,
                service.StartMode != ServiceStartModeKind.Disabled,
                service.ServiceAccount,
                service.DelayedAutoStart == true ? "Automatic delayed service start" : $"{service.StartMode} service start",
                "Services",
                null,
                null));
        }
    }

    private static bool IsStartupTask(ScheduledTaskDefinitionSnapshot task)
    {
        return task.Triggers.Any(IsStartupTrigger);
    }

    private static bool IsStartupTrigger(ScheduledTaskTriggerSnapshot trigger)
    {
        return trigger.Kind is ScheduledTaskTriggerKind.Logon or ScheduledTaskTriggerKind.Boot;
    }

    private static bool IsStartupService(WindowsServiceSnapshot service)
    {
        return service.StartMode is ServiceStartModeKind.Automatic or ServiceStartModeKind.Boot or ServiceStartModeKind.System;
    }

    private sealed record RegistryStartupTarget(
        RegistryHive Hive,
        RegistryView View,
        string KeyPath,
        StartupEntrySourceKind Source,
        string DisplayRoot);
}
