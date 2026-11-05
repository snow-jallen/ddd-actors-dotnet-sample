using Ohmline.Charging.Contracts.ChargePoints;

namespace Ohmline.Charging.Api.Responses;

/// <summary>A registered charge point as the edge shows it: plain JSON values, no types.</summary>
public sealed record ChargePointResponse(
    string ChargePointId,
    long Version,
    string SiteId,
    IReadOnlyList<ConnectorResponse> Connectors)
{
    public static ChargePointResponse From(ChargePointStatus status, string siteId) => new(
        status.ChargePointId.Value,
        status.Version,
        siteId,
        [.. status.Connectors.Select(ConnectorResponse.From)]);
}

/// <summary>One connector: its state is one of the glossary's connector statuses.</summary>
public sealed record ConnectorResponse(
    int Number,
    decimal MaxPowerKw,
    string State,
    string? ReservedFor,
    DateTimeOffset? ReservedUntil,
    Guid? SessionId,
    string? ErrorCode)
{
    public static ConnectorResponse From(ConnectorStatus connector) => new(
        connector.ConnectorNumber,
        connector.MaxPower.Kilowatts,
        connector.State,
        connector.ReservedFor?.Value,
        connector.ReservedUntil,
        connector.SessionId?.Value,
        connector.ErrorCode);
}
