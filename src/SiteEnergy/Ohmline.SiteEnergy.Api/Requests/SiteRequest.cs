namespace Ohmline.SiteEnergy.Api.Requests;

/// <summary>
/// A site as its configuration describes it: the grid connection limit and the safety margin
/// in kW, the IANA time zone, and every connector with its power range and priority class.
/// </summary>
public sealed record SiteRequest(
    string SiteId,
    decimal GridLimitKw,
    decimal SafetyMarginKw,
    string TimeZoneId,
    IReadOnlyList<ConnectorRequest> Connectors);

/// <summary>A connector, such as <c>OHM-SLC-0042/1</c>; its priority is fleet or public.</summary>
public sealed record ConnectorRequest(
    string ConnectorId, decimal MinPowerKw, decimal MaxPowerKw, string Priority);
