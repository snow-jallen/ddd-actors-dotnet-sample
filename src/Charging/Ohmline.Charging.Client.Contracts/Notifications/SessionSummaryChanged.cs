namespace Ohmline.Charging.Client.Contracts.Notifications;

/// <summary>
/// A session's summary changed: pushed to the session's group as <c>sessionSummaryChanged</c>,
/// and the data of each Server-Sent Event of <c>GET /sessions/{id}/events</c>.
/// </summary>
public sealed record SessionSummaryChanged(
    Guid SessionId, long Version, string Status, decimal EnergyKwh);
