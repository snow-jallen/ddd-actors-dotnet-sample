using System.Collections.Immutable;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Tests.Serialization;

/// <summary>
/// One message of every type Charging's serializer writes, with every field filled in, at the
/// bus depot on 2026-11-04 at 07:40 local time. The golden files are these, as JSON.
/// </summary>
internal static class ChargingSamples
{
    public static readonly ChargePointId ChargePoint = new("OHM-SLC-0042");
    public static readonly SiteId Depot = new("slc-depot");
    public static readonly TokenUid Token = new("04A2B3C4D5E6F7");

    public static readonly CommandId Command =
        new(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042"));

    public static readonly SessionId Session =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001"));

    public static readonly DateTimeOffset At = new(2026, 11, 4, 7, 40, 0, TimeSpan.FromHours(-7));

    public static IReadOnlyList<object> All { get; } =
    [
        new RegisterChargePoint(Command, ChargePoint, Depot, [new Power(22m), new Power(22m)]),
        new ReserveConnector(Command, ChargePoint, 1, Token),
        new CancelReservation(Command, ChargePoint, 1, Token),
        new ExpireReservation(Command, ChargePoint, 1),
        new OccupyConnector(Command, ChargePoint, 1, Session, Token),
        new ReleaseConnector(Command, ChargePoint, 1),
        new ReportConnectorFault(Command, ChargePoint, 2, "GroundFailure"),
        new ClearConnectorFault(Command, ChargePoint, 2),
        new MakeConnectorUnavailable(Command, ChargePoint, 2),
        new MakeConnectorAvailable(Command, ChargePoint, 2),

        Stamped(new ChargePointRegistered(ChargePoint, Depot, [new Power(22m), new Power(22m)])),
        Stamped(new ConnectorReserved(ChargePoint, 1, Token, At.AddMinutes(15))),
        Stamped(new ReservationCancelled(ChargePoint, 1, Token)),
        Stamped(new ReservationExpired(ChargePoint, 1, Token)),
        Stamped(new ConnectorOccupied(ChargePoint, 1, Session, Token)),
        Stamped(new ConnectorReleased(ChargePoint, 1, Session)),
        Stamped(new ConnectorFaulted(ChargePoint, 2, "GroundFailure")),
        Stamped(new ConnectorFaultCleared(ChargePoint, 2)),
        Stamped(new ConnectorMadeUnavailable(ChargePoint, 2)),
        Stamped(new ConnectorMadeAvailable(ChargePoint, 2)),

        new GetChargePointStatus(ChargePoint),
        new ChargePointStatus(
            ChargePoint,
            Version: 2,
            Depot,
            ImmutableArray.Create(
                new ConnectorStatus(
                    1, new Power(22m), ConnectorStatus.Reserved, Token, At.AddMinutes(15)),
                new ConnectorStatus(2, new Power(22m), ConnectorStatus.Available))),

        new CommandAccepted(Version: 2),
        new CommandRejected(
            "reserved-for-another-token",
            "Connector 1 is reserved for another driver until 2026-11-04 14:55:00Z."),
    ];

    private static ChargePointEvent Stamped(ChargePointEvent @event) =>
        @event with { CommandId = Command, OccurredAt = At };
}
