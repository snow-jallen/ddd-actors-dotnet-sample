using Akka.Actor;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Api;

/// <summary>
/// Where a connector is, as its charge point knows it: on a site, with the most power it can
/// draw; not registered; or unknown, because the charge point did not answer in time. Starting
/// a journey needs the first, from <c>/journeys</c> and from the driver app's
/// <c>/app/v1/journeys</c> alike.
/// </summary>
internal union ConnectorPlacement(ConnectorPlaced, ConnectorNotRegistered, ChargePointSilent)
{
    public static async Task<ConnectorPlacement> FindAsync(
        IActorRef chargePoints,
        ConnectorId connectorId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var status = await Edge.AskAsync<ChargePointStatus>(
            chargePoints,
            new GetChargePointStatus(connectorId.ChargePointId),
            timeout,
            cancellationToken);
        if (status is null)
        {
            return new ChargePointSilent();
        }

        var connector = status.Connectors.FirstOrDefault(
            connector => connector.ConnectorNumber == connectorId.Number);
        return status.SiteId is { } siteId && connector is not null
            ? new ConnectorPlaced(siteId, connector.MaxPower)
            : new ConnectorNotRegistered();
    }
}

internal sealed record ConnectorPlaced(SiteId SiteId, Power MaxPower);

internal sealed record ConnectorNotRegistered;

internal sealed record ChargePointSilent;
