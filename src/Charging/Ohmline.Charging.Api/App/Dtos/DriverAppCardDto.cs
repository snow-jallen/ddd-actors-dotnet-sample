using Ohmline.Charging.Contracts.Live;

namespace Ohmline.Charging.Api.App.Dtos;

/// <summary>
/// A session card as the driver app shows it: from <c>GET /app/v1/tokens/{uid}/cards</c>, from
/// the change feed, and pushed to the token's group. <paramref name="Version"/> is the session's
/// version; an app keeps the card with the highest. Chapter 19 moves the app's DTOs into a
/// published language of their own.
/// </summary>
public sealed record DriverAppCardDto(
    Guid SessionId,
    string TokenUid,
    string Status,
    string ConnectorId,
    string? ChargePointDisplayName,
    decimal EnergyKwh,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long Version)
{
    public static DriverAppCardDto From(DriverCardChanged changed) =>
        new(
            changed.SessionId.Value,
            changed.TokenUid.Value,
            changed.Status,
            changed.ConnectorId,
            changed.ChargePointDisplayName,
            changed.EnergyKwh,
            changed.StartedAt,
            changed.EndedAt,
            changed.Version);
}
