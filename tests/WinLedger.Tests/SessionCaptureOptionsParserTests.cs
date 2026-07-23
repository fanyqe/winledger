using WinLedger.Cli;
using WinLedger.Core.Sessions;
using WinLedger.Domain.Registry;

namespace WinLedger.Tests;

public sealed class SessionCaptureOptionsParserTests
{
    [Fact]
    public void ParseAddsRegistryProfileTargetsAndEnablesRegistrySubsystem()
    {
        var options = SessionCaptureOptionsParser.Parse(["--registry-profile", "installer"]);

        Assert.Contains(TrackingSubsystemKind.Registry, options.Subsystems);
        Assert.NotNull(options.RegistryTargets);
        Assert.Contains(options.RegistryTargets, target => target.Path.Hive == RegistryHiveKind.CurrentUser);
        Assert.Contains(options.RegistryTargets, target => target.Path.Hive == RegistryHiveKind.LocalMachine);
    }

    [Fact]
    public void ParseUsesDefaultRegistryProfileWhenRegistrySubsystemIsRequested()
    {
        var options = SessionCaptureOptionsParser.Parse(["--subsystems", "registry"]);

        Assert.Equal([TrackingSubsystemKind.Registry], options.Subsystems);
        Assert.NotNull(options.RegistryTargets);
        Assert.NotEmpty(options.RegistryTargets);
    }

    [Fact]
    public void ParseCombinesProfileAndCustomRegistryPath()
    {
        var options = SessionCaptureOptionsParser.Parse(
            ["--registry-profile", "sandbox", "--registry-path", @"HKCU\Software\WinLedger\Extra"]);

        Assert.Contains(options.RegistryTargets!, target =>
            string.Equals(target.Path.KeyPath, @"Software\WinLedger\TestSandbox", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(options.RegistryTargets!, target =>
            string.Equals(target.Path.KeyPath, @"Software\WinLedger\Extra", StringComparison.OrdinalIgnoreCase));
    }
}
