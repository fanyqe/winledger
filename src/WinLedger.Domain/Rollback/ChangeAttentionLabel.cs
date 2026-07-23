namespace WinLedger.Domain.Rollback;

public enum ChangeAttentionLabel
{
    Informational,
    Persistent,
    StartupRelated,
    Privileged,
    NetworkRelated,
    SecuritySensitive,
    PotentiallyDestructive,
    RollbackUnavailable
}
