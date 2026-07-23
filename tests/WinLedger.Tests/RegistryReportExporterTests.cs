using System.Text.Json;
using WinLedger.Comparison.Registry;
using WinLedger.Core.Registry;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Registry;

namespace WinLedger.Tests;

public sealed class RegistryReportExporterTests
{
    [Fact]
    public void ExportJsonUsesStableTopLevelSchemaVersion()
    {
        var sessionId = Guid.NewGuid();
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var baseline = RegistrySnapshot.Empty(sessionId, "Baseline", DateTimeOffset.UtcNow) with
        {
            Keys = [new RegistryKeySnapshot(keyPath, [])]
        };
        var after = baseline with
        {
            Id = Guid.NewGuid(),
            Keys = [new RegistryKeySnapshot(keyPath, [new RegistryValueSnapshot("Setting", RegistryValueType.String, "\"value\"", "value")])]
        };
        var comparison = new RegistrySnapshotComparer().Compare(baseline, after, DateTimeOffset.UtcNow);
        var plan = new RegistryRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new RegistryReportExporter().ExportJson(comparison, plan);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
    }

    [Fact]
    public void ExportTextIncludesReadableChangesAndRollbackCounts()
    {
        var sessionId = Guid.NewGuid();
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var baseline = RegistrySnapshot.Empty(sessionId, "Baseline", DateTimeOffset.UtcNow) with
        {
            Keys = [new RegistryKeySnapshot(keyPath, [])]
        };
        var after = baseline with
        {
            Id = Guid.NewGuid(),
            Keys = [new RegistryKeySnapshot(keyPath, [StringValue("Setting", "value")])]
        };
        var comparison = new RegistrySnapshotComparer().Compare(baseline, after, DateTimeOffset.UtcNow);
        var plan = new RegistryRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var text = new RegistryReportExporter().ExportText(comparison, plan);

        Assert.Contains("WinLedger Registry Report", text, StringComparison.Ordinal);
        Assert.Contains("Changes: 1", text, StringComparison.Ordinal);
        Assert.Contains("Rollback operations: 1", text, StringComparison.Ordinal);
        Assert.Contains("ValueCreated", text, StringComparison.Ordinal);
        Assert.Contains(@"HKEY_CURRENT_USER\Software\WinLedger\TestSandbox\Setting", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportRegWritesRegistryEditorRollbackOperations()
    {
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox", RegistryViewKind.Registry64);
        var comparison = EmptyComparison();
        var plan = new RegistryRollbackPlan(
            Guid.NewGuid(),
            comparison.Id,
            DateTimeOffset.UtcNow,
            [
                Operation(RollbackOperationKind.SetRegistryValue, keyPath, string.Empty, StringValue(string.Empty, "He said \"hello\" \\ path")),
                Operation(RollbackOperationKind.SetRegistryValue, keyPath, "Expand", Value("Expand", RegistryValueType.ExpandString, "%SystemRoot%")),
                Operation(RollbackOperationKind.SetRegistryValue, keyPath, "Multi", Value("Multi", RegistryValueType.MultiString, new[] { "one", "two" })),
                Operation(RollbackOperationKind.SetRegistryValue, keyPath, "Binary", BinaryValue("Binary", [0x01, 0x2a, 0xff])),
                Operation(RollbackOperationKind.SetRegistryValue, keyPath, "Dword", Value("Dword", RegistryValueType.DWord, 42L)),
                Operation(RollbackOperationKind.SetRegistryValue, keyPath, "Qword", Value("Qword", RegistryValueType.QWord, 0x0102030405060708L)),
                Operation(RollbackOperationKind.SetRegistryValue, keyPath, "None", NoneValue("None", [0xde, 0xad])),
                Operation(RollbackOperationKind.DeleteRegistryValue, keyPath, "DeleteMe", null)
            ],
            ["Manual review warning"]);

        var reg = new RegistryReportExporter().ExportReg(comparison, plan);

        Assert.StartsWith("Windows Registry Editor Version 5.00", reg, StringComparison.Ordinal);
        Assert.Contains("; Warning: Manual review warning", reg, StringComparison.Ordinal);
        Assert.Contains("; Registry view: Registry64.", reg, StringComparison.Ordinal);
        Assert.Contains(@"[HKEY_CURRENT_USER\Software\WinLedger\TestSandbox]", reg, StringComparison.Ordinal);
        Assert.DoesNotContain(@"[HKEY_CURRENT_USER\Software\WinLedger\TestSandbox [Registry64]]", reg, StringComparison.Ordinal);
        Assert.Contains("@=\"He said \\\"hello\\\" \\\\ path\"", reg, StringComparison.Ordinal);
        Assert.Contains("\"Expand\"=hex(2):25,00,53,00,79,00,73,00,74,00,65,00,6d,00,52,00,6f,00,6f,00,74,00,25,00,00,00", reg, StringComparison.Ordinal);
        Assert.Contains("\"Multi\"=hex(7):6f,00,6e,00,65,00,00,00,74,00,77,00,6f,00,00,00,00,00", reg, StringComparison.Ordinal);
        Assert.Contains("\"Binary\"=hex:01,2a,ff", reg, StringComparison.Ordinal);
        Assert.Contains("\"Dword\"=dword:0000002a", reg, StringComparison.Ordinal);
        Assert.Contains("\"Qword\"=hex(b):08,07,06,05,04,03,02,01", reg, StringComparison.Ordinal);
        Assert.Contains("\"None\"=hex(0):de,ad", reg, StringComparison.Ordinal);
        Assert.Contains("\"DeleteMe\"=-", reg, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportRegLeavesUnsupportedValueTypesAsComments()
    {
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var comparison = EmptyComparison();
        var plan = new RegistryRollbackPlan(
            Guid.NewGuid(),
            comparison.Id,
            DateTimeOffset.UtcNow,
            [Operation(RollbackOperationKind.SetRegistryValue, keyPath, "Unknown", Value("Unknown", RegistryValueType.Unknown, "raw"))],
            []);

        var reg = new RegistryReportExporter().ExportReg(comparison, plan);

        Assert.Contains("Skipped:", reg, StringComparison.Ordinal);
        Assert.Contains("unsupported value type Unknown", reg, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Unknown\"=", reg, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportPowerShellEmbedsJsonRollbackReportAndCliCommand()
    {
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var comparison = EmptyComparison();
        var operation = Operation(
            RollbackOperationKind.SetRegistryValue,
            keyPath,
            "Setting",
            StringValue("Setting", "before"));
        var plan = new RegistryRollbackPlan(
            Guid.NewGuid(),
            comparison.Id,
            DateTimeOffset.UtcNow,
            [operation],
            ["Review before applying"]);

        var script = new RegistryReportExporter().ExportPowerShell(comparison, plan);

        Assert.Contains("#requires -Version 5.1", script, StringComparison.Ordinal);
        Assert.Contains("registry-rollback-apply", script, StringComparison.Ordinal);
        Assert.Contains(operation.Id.ToString(), script, StringComparison.Ordinal);
        Assert.Contains("Review before applying", script, StringComparison.Ordinal);
        Assert.Contains("WinLedger CLI performs the expected-current-state validation", script, StringComparison.Ordinal);

        var embeddedJson = DecodeEmbeddedReportJson(script);
        using var document = JsonDocument.Parse(embeddedJson);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
        Assert.Equal(operation.Id, document.RootElement.GetProperty("rollbackPlan")[0].GetProperty("id").GetGuid());
    }

    private static string DecodeEmbeddedReportJson(string script)
    {
        const string startMarker = "$ReportJsonBase64 = @'";
        const string endMarker = "'@";
        var startIndex = script.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0);

        startIndex = script.IndexOf('\n', startIndex) + 1;
        var endIndex = script.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex);

        var base64 = script[startIndex..endIndex].ReplaceLineEndings(string.Empty);
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    private static RegistryComparison EmptyComparison()
    {
        return new RegistryComparison(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [],
            []);
    }

    private static RegistryRollbackOperation Operation(
        RollbackOperationKind kind,
        RegistryPath keyPath,
        string valueName,
        RegistryValueSnapshot? restoreValue)
    {
        return new RegistryRollbackOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            keyPath,
            valueName,
            null,
            restoreValue,
            false,
            false);
    }

    private static RegistryValueSnapshot StringValue(string name, string value)
    {
        return Value(name, RegistryValueType.String, value);
    }

    private static RegistryValueSnapshot BinaryValue(string name, byte[] value)
    {
        return new RegistryValueSnapshot(
            name,
            RegistryValueType.Binary,
            JsonSerializer.Serialize(Convert.ToBase64String(value)),
            $"Binary ({value.Length} bytes)");
    }

    private static RegistryValueSnapshot NoneValue(string name, byte[] value)
    {
        return new RegistryValueSnapshot(
            name,
            RegistryValueType.None,
            JsonSerializer.Serialize(Convert.ToBase64String(value)),
            $"None ({value.Length} bytes)");
    }

    private static RegistryValueSnapshot Value<T>(string name, RegistryValueType type, T value)
    {
        return new RegistryValueSnapshot(
            name,
            type,
            JsonSerializer.Serialize(value),
            value?.ToString() ?? string.Empty);
    }
}
