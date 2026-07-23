using Microsoft.CSharp.RuntimeBinder;
using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Windows.ScheduledTasks;

internal static class TaskSchedulerComMapper
{
    public static ScheduledTaskDefinitionSnapshot FromRegisteredTask(dynamic registeredTask)
    {
        string name = registeredTask.Name;
        string fullPath = registeredTask.Path;
        bool enabled = registeredTask.Enabled;
        int stateValue = registeredTask.State;
        string definitionXml = registeredTask.Xml;
        dynamic definition = registeredTask.Definition;
        dynamic principal = definition.Principal;

        var folderPath = ScheduledTaskPath.Split(fullPath).FolderPath;
        var runAsUser = ReadOptionalString(() => principal.UserId);
        var runLevel = ReadOptionalInteger(() => principal.RunLevel);

        return new ScheduledTaskDefinitionSnapshot(
            fullPath,
            folderPath,
            name,
            enabled,
            ToState(stateValue),
            runAsUser,
            ToPrivilegeLevel(runLevel),
            ReadActions(definition.Actions),
            ReadTriggers(definition.Triggers),
            definitionXml);
    }

    private static IReadOnlyList<ScheduledTaskActionSnapshot> ReadActions(dynamic actions)
    {
        var snapshots = new List<ScheduledTaskActionSnapshot>();
        var count = ReadOptionalInteger(() => actions.Count) ?? 0;

        for (var index = 1; index <= count; index++)
        {
            dynamic action = actions.Item(index);
            var kind = ToActionKind(ReadOptionalInteger(() => action.Type));
            var executablePath = kind == ScheduledTaskActionKind.Execute ? ReadOptionalString(() => action.Path) : null;
            var arguments = kind == ScheduledTaskActionKind.Execute ? ReadOptionalString(() => action.Arguments) : null;
            var workingDirectory = kind == ScheduledTaskActionKind.Execute ? ReadOptionalString(() => action.WorkingDirectory) : null;

            snapshots.Add(new ScheduledTaskActionSnapshot(
                kind,
                executablePath,
                arguments,
                workingDirectory,
                CreateActionDetails(kind, executablePath, arguments)));
        }

        return snapshots;
    }

    private static IReadOnlyList<ScheduledTaskTriggerSnapshot> ReadTriggers(dynamic triggers)
    {
        var snapshots = new List<ScheduledTaskTriggerSnapshot>();
        var count = ReadOptionalInteger(() => triggers.Count) ?? 0;

        for (var index = 1; index <= count; index++)
        {
            dynamic trigger = triggers.Item(index);
            var kind = ToTriggerKind(ReadOptionalInteger(() => trigger.Type));
            var enabled = ReadOptionalBoolean(() => trigger.Enabled) ?? false;
            var startBoundary = ReadOptionalString(() => trigger.StartBoundary);
            var endBoundary = ReadOptionalString(() => trigger.EndBoundary);

            snapshots.Add(new ScheduledTaskTriggerSnapshot(
                kind,
                enabled,
                startBoundary,
                endBoundary,
                CreateTriggerDetails(kind, enabled, startBoundary)));
        }

        return snapshots;
    }

    private static ScheduledTaskStateKind ToState(int state)
    {
        return state switch
        {
            1 => ScheduledTaskStateKind.Disabled,
            2 => ScheduledTaskStateKind.Queued,
            3 => ScheduledTaskStateKind.Ready,
            4 => ScheduledTaskStateKind.Running,
            _ => ScheduledTaskStateKind.Unknown
        };
    }

    private static ScheduledTaskPrivilegeLevelKind ToPrivilegeLevel(int? runLevel)
    {
        return runLevel switch
        {
            0 => ScheduledTaskPrivilegeLevelKind.LeastPrivilege,
            1 => ScheduledTaskPrivilegeLevelKind.HighestAvailable,
            _ => ScheduledTaskPrivilegeLevelKind.Unknown
        };
    }

    private static ScheduledTaskActionKind ToActionKind(int? actionType)
    {
        return actionType switch
        {
            0 => ScheduledTaskActionKind.Execute,
            5 => ScheduledTaskActionKind.ComHandler,
            6 => ScheduledTaskActionKind.SendEmail,
            7 => ScheduledTaskActionKind.ShowMessage,
            _ => ScheduledTaskActionKind.Unknown
        };
    }

    private static ScheduledTaskTriggerKind ToTriggerKind(int? triggerType)
    {
        return triggerType switch
        {
            0 => ScheduledTaskTriggerKind.Event,
            1 => ScheduledTaskTriggerKind.Once,
            2 => ScheduledTaskTriggerKind.Daily,
            3 => ScheduledTaskTriggerKind.Weekly,
            4 => ScheduledTaskTriggerKind.Monthly,
            5 => ScheduledTaskTriggerKind.MonthlyDayOfWeek,
            6 => ScheduledTaskTriggerKind.Idle,
            7 => ScheduledTaskTriggerKind.Registration,
            8 => ScheduledTaskTriggerKind.Boot,
            9 => ScheduledTaskTriggerKind.Logon,
            11 => ScheduledTaskTriggerKind.SessionStateChange,
            _ => ScheduledTaskTriggerKind.Unknown
        };
    }

    private static string CreateActionDetails(ScheduledTaskActionKind kind, string? executablePath, string? arguments)
    {
        if (kind != ScheduledTaskActionKind.Execute)
        {
            return kind.ToString();
        }

        return string.IsNullOrWhiteSpace(arguments)
            ? executablePath ?? "(empty executable)"
            : $"{executablePath} {arguments}";
    }

    private static string CreateTriggerDetails(ScheduledTaskTriggerKind kind, bool enabled, string? startBoundary)
    {
        var boundary = string.IsNullOrWhiteSpace(startBoundary) ? string.Empty : $" from {startBoundary}";
        var state = enabled ? "enabled" : "disabled";
        return $"{kind} trigger {state}{boundary}";
    }

    private static string? ReadOptionalString(Func<dynamic> read)
    {
        try
        {
            var value = read();
            return string.IsNullOrWhiteSpace(value as string) ? null : value;
        }
        catch (Exception ex) when (IsComReadException(ex))
        {
            return null;
        }
    }

    private static int? ReadOptionalInteger(Func<dynamic> read)
    {
        try
        {
            var value = read();
            return value is null ? null : Convert.ToInt32(value);
        }
        catch (Exception ex) when (IsComReadException(ex))
        {
            return null;
        }
    }

    private static bool? ReadOptionalBoolean(Func<dynamic> read)
    {
        try
        {
            var value = read();
            return value is null ? null : Convert.ToBoolean(value);
        }
        catch (Exception ex) when (IsComReadException(ex))
        {
            return null;
        }
    }

    private static bool IsComReadException(Exception ex)
    {
        return ex is InvalidOperationException or RuntimeBinderException or System.Runtime.InteropServices.COMException;
    }
}
