namespace Ohmline.Charging.Api.Requests;

/// <summary>
/// Start charging: a token on a connector, such as <c>OHM-SLC-0042/1</c>. The edge finds the
/// connector's site and power; how much is held on the card is the edge's setting.
/// </summary>
public sealed record StartChargingRequest(string TokenUid, string ConnectorId);
