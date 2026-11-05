namespace Ohmline.Charging.Client.Contracts.ReadModels;

/// <summary>
/// A session as the driver app shows it on a card: from <c>GET /app/v1/tokens/{uid}/cards</c>,
/// from the token's feed, and pushed as <c>driverCardChanged</c>. <paramref name="Version"/> is
/// the session's version; an app keeps the card with the highest.
/// </summary>
public sealed record DriverSessionCard(
    Guid SessionId,
    string TokenUid,
    string Status,
    string ConnectorId,
    string? ChargePointDisplayName,
    decimal EnergyKwh,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long Version);
