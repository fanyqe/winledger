namespace WinLedger.Domain.Rollback;

public enum RollbackAvailability
{
    Automatic,
    RequiresConfirmation,
    ManualReview,
    Unavailable
}
