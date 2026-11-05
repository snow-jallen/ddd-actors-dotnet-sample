using System.Collections.Immutable;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.ReadModels.SiteLiveBoard;

/// <summary>
/// What a charge point event changes on the site live board: a connector's status, and the
/// session on it. Only a registration names the site, so only a registration adds rows.
/// </summary>
public sealed record ConnectorStatusChange(
    string ConnectorId, string? SiteId, string Status, Guid? SessionId)
{
    /// <summary>
    /// The board's rules for one charge point event, a pure function: the same statuses
    /// <c>ChargePoint.Evolve</c> gives its connectors. A listing event changes nothing here.
    /// </summary>
    public static ImmutableArray<ConnectorStatusChange> Of(ChargePointEvent @event) =>
        @event switch
        {
            ChargePointRegistered registered =>
            [
                .. Enumerable.Range(1, registered.ConnectorMaxPowers.Length).Select(number =>
                    new ConnectorStatusChange(
                        new ConnectorId(registered.ChargePointId, number).ToString(),
                        registered.SiteId.Value,
                        ConnectorStatus.Available,
                        SessionId: null)),
            ],
            ConnectorReserved reserved => [Of(reserved, ConnectorStatus.Reserved)],
            ReservationCancelled cancelled => [Of(cancelled, ConnectorStatus.Available)],
            ReservationExpired expired => [Of(expired, ConnectorStatus.Available)],
            ConnectorOccupied occupied =>
                [Of(occupied, ConnectorStatus.Occupied, occupied.SessionId.Value)],
            ConnectorReleased released => [Of(released, ConnectorStatus.Available)],
            ConnectorFaulted faulted => [Of(faulted, ConnectorStatus.Faulted)],
            ConnectorFaultCleared cleared => [Of(cleared, ConnectorStatus.Available)],
            ConnectorMadeUnavailable closed => [Of(closed, ConnectorStatus.Unavailable)],
            ConnectorMadeAvailable opened => [Of(opened, ConnectorStatus.Available)],
        };

    private static ConnectorStatusChange Of(
        ConnectorEvent @event, string status, Guid? sessionId = null) =>
        new(
            new ConnectorId(@event.ChargePointId, @event.ConnectorNumber).ToString(),
            SiteId: null,
            status,
            sessionId);
}
