namespace Ohmline.Charging.Api.Requests;

/// <summary>A new charge point: its id, its site and the maximum power of each connector.</summary>
public sealed record RegisterChargePointRequest(
    string ChargePointId, string SiteId, IReadOnlyList<decimal> ConnectorMaxPowersKw);
