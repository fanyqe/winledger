using WinLedger.Domain.Sessions;

namespace WinLedger.App.ViewModels;

public sealed class TrackingSessionListItem(TrackingSession session)
{
    public TrackingSession Session { get; } = session;

    public Guid Id => Session.Id;

    public string Title => Session.Title;

    public string Status => Session.Status.ToString();

    public string CreatedAtUtc => Session.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string DisplayName => $"{Title} - {Status} - {CreatedAtUtc} UTC";
}
