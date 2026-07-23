namespace WinLedger.Domain.Sessions;

public sealed record TrackingSession(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset CreatedAt,
    string WindowsVersion,
    string Architecture,
    string UserSidHash,
    bool IsAdministrator,
    TrackingSessionStatus Status);
