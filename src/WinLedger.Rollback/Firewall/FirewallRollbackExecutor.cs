using WinLedger.Core.Firewall;
using WinLedger.Domain.Firewall;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.Firewall;

public sealed class FirewallRollbackExecutor(IFirewallMutationProvider mutations)
{
    public async Task<IReadOnlyList<FirewallRollbackResult>> ApplyAsync(
        FirewallRollbackPlan plan,
        IReadOnlySet<Guid> selectedOperationIds,
        CancellationToken cancellationToken)
    {
        var results = new List<FirewallRollbackResult>();

        foreach (var operation in plan.Operations.Where(operation => selectedOperationIds.Contains(operation.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<FirewallRollbackResult> ValidateAsync(
        FirewallRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var currentRules = await mutations.ReadRulesByNameAsync(operation.RuleName, cancellationToken)
            .ConfigureAwait(false);

        if (currentRules.Count == 0)
        {
            return new FirewallRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The firewall rule could not be found. Rollback was not applied.");
        }

        if (currentRules.Count > 1)
        {
            return new FirewallRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "Multiple firewall rules share the same name. Rollback was not applied.");
        }

        if (!RulesMatch(currentRules[0], operation.ExpectedCurrentRule))
        {
            return new FirewallRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The firewall rule changed after the comparison snapshot. Rollback was not applied.");
        }

        return new FirewallRollbackResult(
            operation.Id,
            true,
            RollbackValidationState.Valid,
            "The current firewall rule matches the tracked post-change state.");
    }

    private async Task<FirewallRollbackResult> ApplyOperationAsync(
        FirewallRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(operation, cancellationToken).ConfigureAwait(false);
        if (!validation.Succeeded)
        {
            return validation;
        }

        try
        {
            switch (operation.Kind)
            {
                case FirewallRollbackOperationKind.DeleteFirewallRule:
                    await mutations.DeleteRuleAsync(operation.RuleName, cancellationToken).ConfigureAwait(false);
                    break;

                case FirewallRollbackOperationKind.SetFirewallRuleEnabled:
                    if (operation.RestoreEnabled is null)
                    {
                        return MissingRestoreValue(operation.Id);
                    }

                    await mutations.SetRuleEnabledAsync(operation.RuleName, operation.RestoreEnabled.Value, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    return new FirewallRollbackResult(
                        operation.Id,
                        false,
                        RollbackValidationState.Failed,
                        $"Unsupported firewall rollback operation: {operation.Kind}");
            }

            return new FirewallRollbackResult(
                operation.Id,
                true,
                RollbackValidationState.Valid,
                "Rollback operation completed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException or System.Runtime.InteropServices.COMException)
        {
            return new FirewallRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                ex.Message);
        }
    }

    private static bool RulesMatch(WindowsFirewallRuleSnapshot current, WindowsFirewallRuleSnapshot expected)
    {
        return string.Equals(current.Name, expected.Name, StringComparison.Ordinal) &&
               string.Equals(current.Description, expected.Description, StringComparison.Ordinal) &&
               string.Equals(current.ApplicationPath, expected.ApplicationPath, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.ServiceName, expected.ServiceName, StringComparison.OrdinalIgnoreCase) &&
               current.Protocol == expected.Protocol &&
               current.ProtocolNumber == expected.ProtocolNumber &&
               string.Equals(current.LocalPorts, expected.LocalPorts, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.RemotePorts, expected.RemotePorts, StringComparison.OrdinalIgnoreCase) &&
               current.Direction == expected.Direction &&
               current.Action == expected.Action &&
               current.Enabled == expected.Enabled &&
               current.Profiles == expected.Profiles &&
               current.ProfileNames.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(expected.ProfileNames.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase) &&
               string.Equals(current.LocalAddresses, expected.LocalAddresses, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.RemoteAddresses, expected.RemoteAddresses, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.InterfaceTypes, expected.InterfaceTypes, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.IcmpTypesAndCodes, expected.IcmpTypesAndCodes, StringComparison.OrdinalIgnoreCase) &&
               current.EdgeTraversal == expected.EdgeTraversal &&
               string.Equals(current.Grouping, expected.Grouping, StringComparison.Ordinal);
    }

    private static FirewallRollbackResult MissingRestoreValue(Guid operationId)
    {
        return new FirewallRollbackResult(
            operationId,
            false,
            RollbackValidationState.Failed,
            "Rollback operation does not contain a value to restore.");
    }
}
