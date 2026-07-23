namespace WinLedger.Domain.Rollback;

public enum RollbackValidationState
{
    NotValidated,
    Valid,
    Conflict,
    Failed
}
