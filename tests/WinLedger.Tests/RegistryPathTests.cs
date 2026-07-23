using WinLedger.Domain.Registry;

namespace WinLedger.Tests;

public sealed class RegistryPathTests
{
    [Fact]
    public void ParseReadsRegistryViewSuffixWrittenByToString()
    {
        var original = new RegistryPath(
            RegistryHiveKind.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            RegistryViewKind.Registry64);

        var parsed = RegistryPath.Parse(original.ToString());

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void ParseAcceptsShortRegistryViewSuffixes()
    {
        var path = RegistryPath.Parse(@"HKLM\SOFTWARE\Classes [32]");

        Assert.Equal(RegistryHiveKind.LocalMachine, path.Hive);
        Assert.Equal(@"SOFTWARE\Classes", path.KeyPath);
        Assert.Equal(RegistryViewKind.Registry32, path.View);
    }
}
