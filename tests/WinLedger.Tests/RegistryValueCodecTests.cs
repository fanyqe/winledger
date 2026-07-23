using System.Text.Json;
using Microsoft.Win32;
using WinLedger.Domain.Registry;
using WinLedger.Windows.Registry;

namespace WinLedger.Tests;

public sealed class RegistryValueCodecTests
{
    [Fact]
    public void FromWindowsValuePreservesRegNoneBytes()
    {
        var bytes = new byte[] { 0x01, 0x2a, 0xff };

        var snapshot = RegistryValueCodec.FromWindowsValue("Raw", bytes, RegistryValueKind.None);

        Assert.Equal(RegistryValueType.None, snapshot.ValueType);
        Assert.Equal(Convert.ToBase64String(bytes), JsonSerializer.Deserialize<string>(snapshot.SerializedValue));
        Assert.Equal("None (3 bytes)", snapshot.DisplayValue);
    }

    [Fact]
    public void ToWindowsValueRestoresRegNoneBytes()
    {
        var bytes = new byte[] { 0x10, 0x20, 0x30 };
        var snapshot = new RegistryValueSnapshot(
            "Raw",
            RegistryValueType.None,
            JsonSerializer.Serialize(Convert.ToBase64String(bytes)),
            "None (3 bytes)");

        var windowsValue = RegistryValueCodec.ToWindowsValue(snapshot);

        Assert.Equal(RegistryValueKind.None, windowsValue.Kind);
        Assert.Equal(bytes, Assert.IsType<byte[]>(windowsValue.Value));
    }

    [Fact]
    public void ToWindowsValueKeepsLegacyEmptyRegNoneSnapshotsCompatible()
    {
        var snapshot = new RegistryValueSnapshot(
            "Raw",
            RegistryValueType.None,
            JsonSerializer.Serialize(string.Empty),
            string.Empty);

        var windowsValue = RegistryValueCodec.ToWindowsValue(snapshot);

        Assert.Equal(RegistryValueKind.None, windowsValue.Kind);
        Assert.Empty(Assert.IsType<byte[]>(windowsValue.Value));
    }
}
