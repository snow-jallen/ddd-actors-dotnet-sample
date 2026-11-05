using System.Collections.Immutable;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// From the charge point's state to its snapshot and back. Both directions are exhaustive, and
/// the round trip gives back an equal state, which the snapshot tests check for every case.
/// </summary>
public static class ChargePointSnapshotMapping
{
    extension(ChargePointState state)
    {
        /// <summary>Null before registration: there is nothing to keep.</summary>
        public ChargePointSnapshot? ToSnapshot() => state switch
        {
            Unregistered => null,
            Registered registered => new(
                registered.ChargePointId,
                registered.SiteId,
                [
                    .. registered.Connectors
                        .OrderBy(pair => pair.Key)
                        .Select(pair => Of(pair.Value)),
                ]),
            null => throw new InvalidOperationException("The state is default: it holds no case."),
        };
    }

    extension(ChargePointSnapshot snapshot)
    {
        public ChargePointState ToState() => new Registered(
            snapshot.ChargePointId,
            snapshot.SiteId,
            snapshot.Connectors.ToImmutableDictionary(
                connector => connector.Number,
                connector => new Connector(
                    new ConnectorId(snapshot.ChargePointId, connector.Number),
                    connector.MaxPower,
                    StateOf(connector))));
    }

    private static ConnectorSnapshot Of(Connector connector) => connector.State switch
    {
        Available => new ConnectorSnapshot.Available(connector.Id.Number, connector.MaxPower),
        Reserved held => new ConnectorSnapshot.Reserved(
            connector.Id.Number, connector.MaxPower, held.TokenUid, held.ExpiresAt),
        Occupied occupied => new ConnectorSnapshot.Occupied(
            connector.Id.Number, connector.MaxPower, occupied.SessionId),
        Faulted fault => new ConnectorSnapshot.Faulted(
            connector.Id.Number, connector.MaxPower, fault.ErrorCode),
        Unavailable => new ConnectorSnapshot.Unavailable(connector.Id.Number, connector.MaxPower),
        null => throw new InvalidOperationException("The connector's state is default."),
    };

    private static ConnectorState StateOf(ConnectorSnapshot connector) => connector switch
    {
        ConnectorSnapshot.Available => new Available(),
        ConnectorSnapshot.Reserved held => new Reserved(held.TokenUid, held.ExpiresAt),
        ConnectorSnapshot.Occupied occupied => new Occupied(occupied.SessionId),
        ConnectorSnapshot.Faulted fault => new Faulted(fault.ErrorCode),
        ConnectorSnapshot.Unavailable => new Unavailable(),
    };
}
