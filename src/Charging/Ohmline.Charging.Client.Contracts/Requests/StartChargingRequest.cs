namespace Ohmline.Charging.Client.Contracts.Requests;

/// <summary>
/// <c>POST /app/v1/journeys</c>: charge on a connector, given in its printed form
/// (<c>OHM-SLC-0042/1</c>), with a token, holding at most <paramref name="SpendCapAmount"/> in
/// <paramref name="Currency"/> (an ISO 4217 code). The app makes both ids at the tap, so the same
/// request sent again names the same command and the same journey.
/// </summary>
public sealed record StartChargingRequest(
    Guid CommandId,
    Guid JourneyId,
    string ConnectorId,
    string TokenUid,
    decimal SpendCapAmount,
    string Currency);
