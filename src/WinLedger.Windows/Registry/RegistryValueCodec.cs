using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;
using WinLedger.Domain.Registry;

namespace WinLedger.Windows.Registry;

public static class RegistryValueCodec
{
    public static RegistryValueSnapshot FromWindowsValue(string name, object? value, RegistryValueKind kind)
    {
        var type = ToDomainType(kind);
        var serialized = Serialize(value, type);
        var display = ToDisplayValue(value, type);

        return new RegistryValueSnapshot(name, type, serialized, display);
    }

    public static (object Value, RegistryValueKind Kind) ToWindowsValue(RegistryValueSnapshot snapshot)
    {
        return snapshot.ValueType switch
        {
            RegistryValueType.String => (JsonSerializer.Deserialize<string>(snapshot.SerializedValue) ?? string.Empty, RegistryValueKind.String),
            RegistryValueType.ExpandString => (JsonSerializer.Deserialize<string>(snapshot.SerializedValue) ?? string.Empty, RegistryValueKind.ExpandString),
            RegistryValueType.MultiString => (JsonSerializer.Deserialize<string[]>(snapshot.SerializedValue) ?? Array.Empty<string>(), RegistryValueKind.MultiString),
            RegistryValueType.Binary => (Convert.FromBase64String(JsonSerializer.Deserialize<string>(snapshot.SerializedValue) ?? string.Empty), RegistryValueKind.Binary),
            RegistryValueType.DWord => ((int)JsonSerializer.Deserialize<long>(snapshot.SerializedValue), RegistryValueKind.DWord),
            RegistryValueType.QWord => (JsonSerializer.Deserialize<long>(snapshot.SerializedValue), RegistryValueKind.QWord),
            RegistryValueType.None => (Convert.FromBase64String(JsonSerializer.Deserialize<string>(snapshot.SerializedValue) ?? string.Empty), RegistryValueKind.None),
            _ => throw new InvalidOperationException($"Unsupported registry value type '{snapshot.ValueType}'.")
        };
    }

    public static RegistryValueType ToDomainType(RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.String => RegistryValueType.String,
            RegistryValueKind.ExpandString => RegistryValueType.ExpandString,
            RegistryValueKind.Binary => RegistryValueType.Binary,
            RegistryValueKind.DWord => RegistryValueType.DWord,
            RegistryValueKind.MultiString => RegistryValueType.MultiString,
            RegistryValueKind.QWord => RegistryValueType.QWord,
            RegistryValueKind.None => RegistryValueType.None,
            _ => RegistryValueType.Unknown
        };
    }

    private static string Serialize(object? value, RegistryValueType type)
    {
        return type switch
        {
            RegistryValueType.String or RegistryValueType.ExpandString => JsonSerializer.Serialize(value?.ToString() ?? string.Empty),
            RegistryValueType.MultiString => JsonSerializer.Serialize(value as string[] ?? Array.Empty<string>()),
            RegistryValueType.Binary or RegistryValueType.None => JsonSerializer.Serialize(Convert.ToBase64String(value as byte[] ?? Array.Empty<byte>())),
            RegistryValueType.DWord or RegistryValueType.QWord => JsonSerializer.Serialize(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            _ => JsonSerializer.Serialize(value?.ToString() ?? string.Empty)
        };
    }

    private static string ToDisplayValue(object? value, RegistryValueType type)
    {
        return type switch
        {
            RegistryValueType.MultiString => string.Join("; ", value as string[] ?? Array.Empty<string>()),
            RegistryValueType.Binary => $"Binary ({(value as byte[] ?? Array.Empty<byte>()).Length} bytes)",
            RegistryValueType.None => $"None ({(value as byte[] ?? Array.Empty<byte>()).Length} bytes)",
            _ => value?.ToString() ?? string.Empty
        };
    }
}
