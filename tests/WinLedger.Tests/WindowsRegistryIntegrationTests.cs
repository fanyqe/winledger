using Microsoft.Win32;
using WinLedger.Comparison.Registry;
using WinLedger.Core.Abstractions;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Registry;
using WinLedger.Windows.Registry;

namespace WinLedger.Tests;

public sealed class WindowsRegistryIntegrationTests
{
    private const string IntegrationRootKeyPath = @"Software\WinLedger\IntegrationTests";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RegistrySnapshotCompareAndRollbackUsesRealCurrentUserSandbox()
    {
        var sessionId = Guid.NewGuid();
        var keyPath = $@"{IntegrationRootKeyPath}\{Guid.NewGuid():N}";
        var registryPath = new RegistryPath(RegistryHiveKind.CurrentUser, keyPath);
        var targets = new[]
        {
            new RegistrySnapshotTarget(registryPath, IncludeSubKeys: true, "Integration sandbox")
        };

        DeleteSandboxKey(keyPath);
        try
        {
            var collector = new WindowsRegistrySnapshotCollector(new FixedClock());
            var baseline = await collector.CaptureAsync(sessionId, "Baseline", targets, CancellationToken.None);

            using (var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true))
            {
                Assert.NotNull(key);
                key.SetValue("Setting", "after", RegistryValueKind.String);
            }

            var comparisonSnapshot = await collector.CaptureAsync(sessionId, "Comparison", targets, CancellationToken.None);
            var comparison = new RegistrySnapshotComparer().Compare(baseline, comparisonSnapshot, DateTimeOffset.UtcNow);

            var change = Assert.Single(
                comparison.Changes,
                item => item.Kind == RegistryChangeKind.ValueCreated && item.ValueName == "Setting");
            Assert.Equal(RollbackAvailability.Automatic, change.RollbackAvailability);

            var plan = new RegistryRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);
            var operation = Assert.Single(plan.Operations, item => item.ValueName == "Setting");
            var results = await new RegistryRollbackExecutor(new WindowsRegistryMutationProvider())
                .ApplyAsync(plan, new HashSet<Guid> { operation.Id }, CancellationToken.None);

            Assert.True(Assert.Single(results).Succeeded);
            using var restoredKey = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
            Assert.NotNull(restoredKey);
            Assert.Null(restoredKey.GetValue("Setting", null));
        }
        finally
        {
            DeleteSandboxKey(keyPath);
        }
    }

    private static void DeleteSandboxKey(string keyPath)
    {
        Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        DeleteIntegrationRootIfEmpty();
    }

    private static void DeleteIntegrationRootIfEmpty()
    {
        using var key = Registry.CurrentUser.OpenSubKey(IntegrationRootKeyPath, writable: false);
        if (key is null || key.GetSubKeyNames().Length > 0 || key.GetValueNames().Length > 0)
        {
            return;
        }

        Registry.CurrentUser.DeleteSubKeyTree(IntegrationRootKeyPath, throwOnMissingSubKey: false);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.Parse(
            "2026-07-24T10:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
