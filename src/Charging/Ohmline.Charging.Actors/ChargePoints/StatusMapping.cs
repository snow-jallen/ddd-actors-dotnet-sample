using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// From the charge point's state, a union that never leaves the actor, to the status it answers
/// with, plain data for the wire. Both switches are exhaustive: a new state case does not compile
/// until the status says how to show it.
/// </summary>
public static class StatusMapping
{
    extension(ChargePointState state)
    {
        public ChargePointStatus ToStatus(ChargePointId chargePointId, long version) =>
            state switch
            {
                Unregistered => new(chargePointId, version, SiteId: null, Connectors: []),
                Registered registered => new(
                    chargePointId,
                    version,
                    registered.SiteId,
                    [.. registered.Connectors.Values
                        .OrderBy(connector => connector.Id.Number)
                        .Select(StatusOf)]),
                null => throw new InvalidOperationException("The state is default."),
            };
    }

    private static ConnectorStatus StatusOf(Connector connector)
    {
        var (number, maxPower) = (connector.Id.Number, connector.MaxPower);
        return connector.State switch
        {
            Available => new(number, maxPower, ConnectorStatus.Available),
            Reserved held => new(
                number, maxPower, ConnectorStatus.Reserved, held.TokenUid, held.ExpiresAt),
            Occupied current => new(
                number, maxPower, ConnectorStatus.Occupied, SessionId: current.SessionId),
            OutOfOrder outOfOrder => new(
                number, maxPower, ConnectorStatus.OutOfOrder, ErrorCode: outOfOrder.ErrorCode),
            Unavailable => new(number, maxPower, ConnectorStatus.Unavailable),
            null => throw new InvalidOperationException("The connector state is default."),
        };
    }
}
