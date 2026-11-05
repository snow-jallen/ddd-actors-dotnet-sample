namespace Ohmline.Charging.Api.Requests;

/// <summary>
/// Start a session. The caller names it with a version 7 Guid, so that sending the request
/// again names the same session. The tariff is named by its id; the edge copies its current
/// version into the session. The spend cap is in the tariff's currency.
/// </summary>
public sealed record StartSessionRequest(
    Guid SessionId,
    string SiteId,
    string ConnectorId,
    string TokenUid,
    string TariffId,
    decimal SpendCap,
    long MeterStartWh);
