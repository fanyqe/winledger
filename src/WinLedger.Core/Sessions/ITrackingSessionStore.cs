using WinLedger.Domain.Sessions;

namespace WinLedger.Core.Sessions;

public interface ITrackingSessionStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task SaveSessionAsync(TrackingSession session, CancellationToken cancellationToken);

    Task<TrackingSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrackingSession>> ListSessionsAsync(CancellationToken cancellationToken);
}
