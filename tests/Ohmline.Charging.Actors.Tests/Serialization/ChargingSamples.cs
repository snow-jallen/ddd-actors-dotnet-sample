using System.Collections.Immutable;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Listings;
using Ohmline.Charging.Domain.Metering;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;

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

    public static readonly ConnectorId Connector = new(ChargePoint, 1);

    /// <summary>A tariff with a component of every kind, so each kind has a golden file.</summary>
    public static readonly TariffSnapshot Tariff = new(
        new TariffId("slc-standard"),
        3,
        Currency.Usd,
        [
            new EnergyRate(
                new Money(0.62m, Currency.Usd),
                new TimeOfUseBand("peak", new TimeOnly(16, 0), new TimeOnly(21, 0))),
            new EnergyRate(new Money(0.45m, Currency.Usd)),
            new TimeRate(new Money(0.02m, Currency.Usd)),
            new SessionFee(new Money(1.00m, Currency.Usd)),
            new IdleFee(new Money(0.40m, Currency.Usd), TimeSpan.FromMinutes(10)),
        ],
        PriceCap: new Money(60m, Currency.Usd),
        RoundingPolicy.Cents,
        "America/Denver");

    public static readonly Money SpendCap = new(40m, Currency.Usd);

    public const string DisplayName = "Salt Lake bus depot, public bays";

    public const string AccessInstructions = "Level -2, bays 14-19. Gate code at the kiosk.";

    public const string OpeningHours = "Mon-Fri 05:00-23:00; Sat-Sun 07:00-21:00";

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

        new StartSession(
            Command, Session, Depot, Connector, Token, Tariff, SpendCap, MeterStartWh: 18_204_000),
        new RecordMeterValue(Command, Session, 18_210_350, At.AddMinutes(10)),
        new SuspendCharging(Command, Session, new ByEv()),
        new ResumeCharging(Command, Session),
        new StopSession(Command, Session),

        Stamped(new SessionStarted(
            Session, Depot, Connector, Token, Tariff, SpendCap, MeterStartWh: 18_204_000)),
        Stamped(new MeterValueRecorded(Session, 18_210_350, At.AddMinutes(10))),
        Stamped(new MeterAnomalyDetected(
            Session, 18_209_000, At.AddMinutes(11), 18_210_350, At.AddMinutes(10))),
        Stamped(new ChargingSuspendedByEv(Session)),
        Stamped(new ChargingSuspendedByCharger(Session)),
        Stamped(new ChargingResumed(Session)),
        Stamped(new SpendCapReached(
            Session, new Money(40.12m, Currency.Usd), SpendCap)),
        Stamped(new SessionEnded(Session, Depot, Connector, Token, new Energy(63.35m))),

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

        // A charge point with a connector in every state, so every kind has a golden file.
        new ChargePointSnapshot(
            ChargePoint,
            Depot,
            [
                new ConnectorSnapshot.Available(1, new Power(22m)),
                new ConnectorSnapshot.Reserved(2, new Power(22m), Token, At.AddMinutes(15)),
                new ConnectorSnapshot.Occupied(3, new Power(150m), Session),
                new ConnectorSnapshot.Faulted(4, new Power(150m), "GroundFailure"),
                new ConnectorSnapshot.Unavailable(5, new Power(7.4m)),
            ]),
        new ChargingSessionSnapshot(
            Session,
            Depot,
            Connector,
            Token,
            Tariff,
            SpendCap,
            new SessionPhaseSnapshot.SuspendedByEv(
                MeterReading.FromWattHours(18_204_000, At),
                MeterReading.FromWattHours(18_210_350, At.AddMinutes(10)),
                Since: At.AddMinutes(12))),

        new ReviseChargePointListing(
            Command, ChargePoint, DisplayName, AccessInstructions, OpeningHours),
        Stamped(new ChargePointListingRevised(
            ChargePoint, DisplayName, AccessInstructions, OpeningHours, Revision: 3)),
        new GetChargePointListing(ChargePoint),
        new ChargePointListingView(
            ChargePoint, Revision: 3, DisplayName, AccessInstructions, OpeningHours),
        new ChargePointListingSnapshot(
            ChargePoint, DisplayName, AccessInstructions, OpeningHours, Revision: 3),
    ];

    private static ChargePointEvent Stamped(ChargePointEvent @event) =>
        @event with { CommandId = Command, OccurredAt = At };

    private static SessionEvent Stamped(SessionEvent @event) =>
        @event with { CommandId = Command, OccurredAt = At };

    private static ChargePointListingEvent Stamped(ChargePointListingEvent @event) =>
        @event with { CommandId = Command, OccurredAt = At };
}
