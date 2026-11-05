namespace Ohmline.Charging.Client.Contracts.Requests;

/// <summary>
/// <c>POST /app/v1/charge-points/{id}/connectors/{n}/reservations</c>: hold the connector the
/// route names for this token, such as <c>04A2B3C4D5E6F7</c>.
/// </summary>
public sealed record ReserveConnectorRequest(Guid CommandId, string TokenUid);
