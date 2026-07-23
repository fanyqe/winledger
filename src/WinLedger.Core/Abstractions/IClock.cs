namespace WinLedger.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
