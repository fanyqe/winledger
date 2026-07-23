using WinLedger.Comparison.EnvironmentVariables;
using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.EnvironmentVariables;

namespace WinLedger.Tests;

public sealed class EnvironmentRollbackPlannerTests
{
    [Fact]
    public void CreatePlanAddsOneOperationPerVariableAndMarksMachineScopeAsAdmin()
    {
        var sessionId = Guid.NewGuid();
        var beforePath = Variable("Path", @"C:\One;C:\Two", EnvironmentVariableScopeKind.Machine);
        var afterPath = Variable("Path", @"C:\Two;C:\One;C:\Added", EnvironmentVariableScopeKind.Machine);
        var beforeUser = Variable("USER_VAR", "before", EnvironmentVariableScopeKind.User);
        var afterUser = Variable("USER_VAR", "after", EnvironmentVariableScopeKind.User);
        var comparison = new EnvironmentSnapshotComparer().Compare(
            Snapshot(sessionId, beforePath, beforeUser),
            Snapshot(sessionId, afterPath, afterUser),
            DateTimeOffset.UtcNow);

        var plan = new EnvironmentRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Equal(2, plan.Operations.Count);
        Assert.Contains(plan.Operations, operation =>
            operation.Name == "Path" &&
            operation.Scope == EnvironmentVariableScopeKind.Machine &&
            operation.Kind == EnvironmentRollbackOperationKind.SetEnvironmentVariable &&
            operation.RequiresAdministrator &&
            operation.RequiresRestart);
        Assert.Contains(plan.Operations, operation =>
            operation.Name == "USER_VAR" &&
            operation.Scope == EnvironmentVariableScopeKind.User &&
            !operation.RequiresAdministrator &&
            operation.RequiresRestart);
    }

    [Fact]
    public void CreatePlanDeletesCreatedVariableAndRestoresRemovedVariable()
    {
        var sessionId = Guid.NewGuid();
        var removed = Variable("REMOVED_VAR", "old", EnvironmentVariableScopeKind.User);
        var created = Variable("CREATED_VAR", "new", EnvironmentVariableScopeKind.User);
        var comparison = new EnvironmentSnapshotComparer().Compare(
            Snapshot(sessionId, removed),
            Snapshot(sessionId, created),
            DateTimeOffset.UtcNow);

        var plan = new EnvironmentRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Contains(plan.Operations, operation =>
            operation.Name == "CREATED_VAR" &&
            operation.Kind == EnvironmentRollbackOperationKind.DeleteEnvironmentVariable &&
            operation.ExpectedCurrentVariable?.RawValue == "new" &&
            operation.RestoreVariable is null);
        Assert.Contains(plan.Operations, operation =>
            operation.Name == "REMOVED_VAR" &&
            operation.Kind == EnvironmentRollbackOperationKind.SetEnvironmentVariable &&
            operation.ExpectedCurrentVariable is null &&
            operation.RestoreVariable?.RawValue == "old");
    }

    [Fact]
    public void CreatePlanSkipsUnsupportedRestoreValueTypes()
    {
        var sessionId = Guid.NewGuid();
        var before = Variable("ODD_VAR", "before", EnvironmentVariableScopeKind.User) with { ValueType = EnvironmentVariableValueType.Unknown };
        var after = Variable("ODD_VAR", "after", EnvironmentVariableScopeKind.User);
        var comparison = new EnvironmentSnapshotComparer().Compare(
            Snapshot(sessionId, before),
            Snapshot(sessionId, after),
            DateTimeOffset.UtcNow);

        var plan = new EnvironmentRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Empty(plan.Operations);
        Assert.Contains(plan.Warnings, warning => warning.Contains("unsupported value type", StringComparison.OrdinalIgnoreCase));
    }

    private static EnvironmentSnapshot Snapshot(Guid sessionId, params EnvironmentVariableSnapshot[] variables)
    {
        return new EnvironmentSnapshot(Guid.NewGuid(), sessionId, "Snapshot", DateTimeOffset.UtcNow, variables, []);
    }

    private static EnvironmentVariableSnapshot Variable(
        string name,
        string value,
        EnvironmentVariableScopeKind scope)
    {
        return new EnvironmentVariableSnapshot(
            scope,
            name,
            value,
            EnvironmentVariableValueType.ExpandString,
            string.Equals(name, "Path", StringComparison.OrdinalIgnoreCase)
                ? value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>(),
            scope == EnvironmentVariableScopeKind.User
                ? @"HKCU\Environment"
                : @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
    }
}
