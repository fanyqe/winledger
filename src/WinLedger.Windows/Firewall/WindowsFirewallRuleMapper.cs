using Microsoft.CSharp.RuntimeBinder;
using WinLedger.Domain.Firewall;

namespace WinLedger.Windows.Firewall;

internal static class WindowsFirewallRuleMapper
{
    public static WindowsFirewallRuleSnapshot FromComRule(dynamic rule, string identity, bool hasDuplicateName)
    {
        int protocolNumber = GetValue<int>(rule, "Protocol");
        int direction = GetValue<int>(rule, "Direction");
        int action = GetValue<int>(rule, "Action");
        int profiles = GetValue<int>(rule, "Profiles");

        return new WindowsFirewallRuleSnapshot(
            identity,
            GetString(rule, "Name") ?? "(unnamed firewall rule)",
            GetString(rule, "Description"),
            GetString(rule, "ApplicationName"),
            GetString(rule, "ServiceName"),
            MapProtocol(protocolNumber),
            protocolNumber,
            GetString(rule, "LocalPorts"),
            GetString(rule, "RemotePorts"),
            MapDirection(direction),
            MapAction(action),
            GetValue<bool>(rule, "Enabled"),
            profiles,
            ProfileNames(profiles),
            GetString(rule, "LocalAddresses"),
            GetString(rule, "RemoteAddresses"),
            GetString(rule, "InterfaceTypes"),
            GetString(rule, "IcmpTypesAndCodes"),
            GetValue<bool>(rule, "EdgeTraversal"),
            GetString(rule, "Grouping"),
            hasDuplicateName);
    }

    public static IReadOnlyList<WindowsFirewallRuleSnapshot> AnnotateDuplicateNames(
        IReadOnlyList<WindowsFirewallRuleSnapshot> rules)
    {
        var duplicateNames = rules
            .GroupBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rules
            .Select(rule => rule with { HasDuplicateName = duplicateNames.Contains(rule.Name) })
            .OrderBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.Direction)
            .ThenBy(rule => rule.ApplicationPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.ProtocolNumber)
            .ThenBy(rule => rule.LocalPorts, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string IdentityFor(dynamic rule, int occurrence)
    {
        var name = GetString(rule, "Name") ?? "(unnamed firewall rule)";
        return $"{name}\u001F{occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    public static bool IsFirewallException(Exception ex)
    {
        return ex is InvalidOperationException
            or UnauthorizedAccessException
            or RuntimeBinderException
            or System.Runtime.InteropServices.COMException;
    }

    private static string? GetString(dynamic rule, string propertyName)
    {
        try
        {
            object? value = GetProperty(rule, propertyName);
            return value?.ToString();
        }
        catch (Exception ex) when (IsFirewallException(ex))
        {
            return null;
        }
    }

    private static T GetValue<T>(dynamic rule, string propertyName)
    {
        try
        {
            object? value = GetProperty(rule, propertyName);
            if (value is T typed)
            {
                return typed;
            }

            if (value is not null)
            {
                return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) when (IsFirewallException(ex) || ex is FormatException or InvalidCastException)
        {
        }

        return default!;
    }

    private static object? GetProperty(dynamic rule, string propertyName)
    {
        return propertyName switch
        {
            "Name" => rule.Name,
            "Description" => rule.Description,
            "ApplicationName" => rule.ApplicationName,
            "ServiceName" => rule.ServiceName,
            "Protocol" => rule.Protocol,
            "LocalPorts" => rule.LocalPorts,
            "RemotePorts" => rule.RemotePorts,
            "Direction" => rule.Direction,
            "Action" => rule.Action,
            "Enabled" => rule.Enabled,
            "Profiles" => rule.Profiles,
            "LocalAddresses" => rule.LocalAddresses,
            "RemoteAddresses" => rule.RemoteAddresses,
            "InterfaceTypes" => rule.InterfaceTypes,
            "IcmpTypesAndCodes" => rule.IcmpTypesAndCodes,
            "EdgeTraversal" => rule.EdgeTraversal,
            "Grouping" => rule.Grouping,
            _ => null
        };
    }

    private static FirewallRuleProtocolKind MapProtocol(int protocol)
    {
        return protocol switch
        {
            1 => FirewallRuleProtocolKind.IcmpV4,
            6 => FirewallRuleProtocolKind.Tcp,
            17 => FirewallRuleProtocolKind.Udp,
            58 => FirewallRuleProtocolKind.IcmpV6,
            256 => FirewallRuleProtocolKind.Any,
            _ => FirewallRuleProtocolKind.Other
        };
    }

    private static FirewallRuleDirectionKind MapDirection(int direction)
    {
        return direction switch
        {
            1 => FirewallRuleDirectionKind.Inbound,
            2 => FirewallRuleDirectionKind.Outbound,
            _ => FirewallRuleDirectionKind.Unknown
        };
    }

    private static FirewallRuleActionKind MapAction(int action)
    {
        return action switch
        {
            0 => FirewallRuleActionKind.Block,
            1 => FirewallRuleActionKind.Allow,
            _ => FirewallRuleActionKind.Unknown
        };
    }

    private static IReadOnlyList<string> ProfileNames(int profiles)
    {
        if (profiles < 0 || profiles == int.MaxValue)
        {
            return ["Domain", "Private", "Public"];
        }

        var names = new List<string>();
        if ((profiles & 1) != 0)
        {
            names.Add("Domain");
        }

        if ((profiles & 2) != 0)
        {
            names.Add("Private");
        }

        if ((profiles & 4) != 0)
        {
            names.Add("Public");
        }

        return names;
    }
}
