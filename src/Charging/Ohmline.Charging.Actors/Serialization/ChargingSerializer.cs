using System.Collections.Frozen;
using System.Text.Json;
using Akka.Actor;
using Ohmline.ActorShell.Serialization;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Actors.Persistence.Legacy;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Chargers;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.Domain.Listings;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Actors.Serialization;

/// <summary>
/// Everything Charging tells, asks, replies or persists, as JSON under a permanent manifest. The
/// identifier and every manifest below are written into the journal beside each event, so they
/// never change and are never reused (ADR 0007). A new type gets a new line; a new shape of an
/// existing type gets a new manifest version, and the old version moves to
/// <see cref="Upcasters"/>, where it is read for ever.
/// </summary>
public sealed class ChargingSerializer(ExtendedActorSystem system)
    : ManifestJsonSerializer(system, Manifests, Upcasters, AddKinds)
{
    /// <summary>The name Akka.NET knows the serializer by, in configuration and logs.</summary>
    public const string Name = "charging";

    /// <summary>Every message whose type derives from one of these is written here.</summary>
    public static IReadOnlyList<Type> BoundTypes { get; } =
    [
        typeof(ChargePointCommand),
        typeof(ChargePointEvent),
        typeof(ChargePointQuery),
        typeof(SessionCommand),
        typeof(SessionEvent),
        typeof(ChargePointStatus),
        typeof(CommandReply),
        typeof(ChargePointSnapshot),
        typeof(ChargingSessionSnapshot),
        typeof(ChargePointListingCommand),
        typeof(ChargePointListingEvent),
        typeof(ChargePointListingQuery),
        typeof(ChargePointListingView),
        typeof(ChargePointListingSnapshot),
        typeof(LiveUpdate),
        typeof(JourneyCommand),
        typeof(JourneyEvent),
        typeof(JourneyQuery),
        typeof(JourneyView),
        typeof(PaymentPortResult),
        typeof(ChargerCommandResult),
        typeof(JourneyTimerElapsed),
        typeof(ChargerCommand),
        typeof(ChargerCommandReply),
        typeof(LegacyChargingEvent),
        typeof(MeterValueRecordedV1),
    ];

    public static FrozenDictionary<Type, string> Manifests { get; } = new Dictionary<Type, string>
    {
        [typeof(RegisterChargePoint)] = "charging.register-charge-point.v1",
        [typeof(ReserveConnector)] = "charging.reserve-connector.v1",
        [typeof(CancelReservation)] = "charging.cancel-reservation.v1",
        [typeof(ExpireReservation)] = "charging.expire-reservation.v1",
        [typeof(OccupyConnector)] = "charging.occupy-connector.v1",
        [typeof(ReleaseConnector)] = "charging.release-connector.v1",
        [typeof(ReportConnectorFault)] = "charging.report-connector-fault.v1",
        [typeof(ClearConnectorFault)] = "charging.clear-connector-fault.v1",
        [typeof(MakeConnectorUnavailable)] = "charging.make-connector-unavailable.v1",
        [typeof(MakeConnectorAvailable)] = "charging.make-connector-available.v1",

        [typeof(ChargePointRegistered)] = "charging.charge-point-registered.v1",
        [typeof(ConnectorReserved)] = "charging.connector-reserved.v1",
        [typeof(ReservationCancelled)] = "charging.reservation-cancelled.v1",
        [typeof(ReservationExpired)] = "charging.reservation-expired.v1",
        [typeof(ConnectorOccupied)] = "charging.connector-occupied.v1",
        [typeof(ConnectorReleased)] = "charging.connector-released.v1",
        [typeof(ConnectorFaulted)] = "charging.connector-faulted.v1",
        [typeof(ConnectorFaultCleared)] = "charging.connector-fault-cleared.v1",
        [typeof(ConnectorMadeUnavailable)] = "charging.connector-made-unavailable.v1",
        [typeof(ConnectorMadeAvailable)] = "charging.connector-made-available.v1",

        [typeof(StartSession)] = "charging.start-session.v1",
        [typeof(RecordMeterValue)] = "charging.record-meter-value.v1",
        [typeof(SuspendCharging)] = "charging.suspend-charging.v1",
        [typeof(ResumeCharging)] = "charging.resume-charging.v1",
        [typeof(StopSession)] = "charging.stop-session.v1",

        [typeof(SessionStarted)] = "charging.session-started.v1",
        [typeof(MeterValueRecorded)] = "charging.meter-value-recorded.v2",
        [typeof(MeterAnomalyDetected)] = "charging.meter-anomaly-detected.v1",
        [typeof(ChargingSuspendedByEv)] = "charging.charging-suspended-by-ev.v1",
        [typeof(ChargingSuspendedByCharger)] = "charging.charging-suspended-by-charger.v1",
        [typeof(ChargingResumed)] = "charging.charging-resumed.v1",
        [typeof(SpendCapReached)] = "charging.spend-cap-reached.v1",
        [typeof(SessionEnded)] = "charging.session-ended.v1",

        [typeof(GetChargePointStatus)] = "charging.get-charge-point-status.v1",
        [typeof(ChargePointStatus)] = "charging.charge-point-status.v1",

        [typeof(CommandAccepted)] = "charging.command-accepted.v1",
        [typeof(CommandRejected)] = "charging.command-rejected.v1",

        [typeof(ChargePointSnapshot)] = "charging.charge-point-snapshot.v1",
        [typeof(ChargingSessionSnapshot)] = "charging.charging-session-snapshot.v1",

        [typeof(ReviseChargePointListing)] = "charging.revise-charge-point-listing.v1",
        [typeof(ChargePointListingRevised)] = "charging.charge-point-listing-revised.v1",
        [typeof(GetChargePointListing)] = "charging.get-charge-point-listing.v1",
        [typeof(ChargePointListingView)] = "charging.charge-point-listing-view.v1",
        [typeof(ChargePointListingSnapshot)] = "charging.charge-point-listing-snapshot.v1",

        [typeof(SessionSummaryChanged)] = "charging.session-summary-changed.v1",
        [typeof(DriverCardChanged)] = "charging.driver-card-changed.v1",

        [typeof(StartCharging)] = "charging.start-charging.v1",
        [typeof(JourneyStarted)] = "charging.journey-started.v1",
        [typeof(TokenSecured)] = "charging.token-secured.v1",
        [typeof(HoldSecured)] = "charging.hold-secured.v1",
        [typeof(PowerSecured)] = "charging.power-secured.v1",
        [typeof(EnergyFlowStarted)] = "charging.energy-flow-started.v1",
        [typeof(SettlementDue)] = "charging.settlement-due.v1",
        [typeof(JourneyCompleted)] = "charging.journey-completed.v1",
        [typeof(JourneyAbandoned)] = "charging.journey-abandoned.v1",
        [typeof(CaptureAttemptFailed)] = "charging.capture-attempt-failed.v1",
        [typeof(DebtRecorded)] = "charging.debt-recorded.v1",
        [typeof(GetJourney)] = "charging.get-journey.v1",
        [typeof(JourneyView)] = "charging.journey-view.v1",
        [typeof(HoldPlaced)] = "charging.hold-placed.v1",
        [typeof(HoldDeclined)] = "charging.hold-declined.v1",
        [typeof(HoldVoided)] = "charging.hold-voided.v1",
        [typeof(PaymentCaptured)] = "charging.payment-captured.v1",
        [typeof(CaptureFailed)] = "charging.capture-failed.v1",
        [typeof(ChargerAccepted)] = "charging.charger-accepted.v1",
        [typeof(ChargerRejected)] = "charging.charger-rejected.v1",
        [typeof(JourneyTimerElapsed)] = "charging.journey-timer-elapsed.v1",

        [typeof(RemoteStart)] = "charging.remote-start.v1",
        [typeof(RemoteStop)] = "charging.remote-stop.v1",
        [typeof(SetChargingProfile)] = "charging.set-charging-profile.v1",
        [typeof(ChargerCommandAccepted)] = "charging.charger-command-accepted.v1",
        [typeof(ChargerCommandRejected)] = "charging.charger-command-rejected.v1",

        // Never written by Charging; read, then turned into today's events by
        // LegacyEventsReadAdapter (section 20.4).
        [typeof(ReservationTakenOver)] = "charging.reservation-taken-over.v1",
        [typeof(ChargePointHeartbeatRecorded)] = "charging.charge-point-heartbeat-recorded.v1",
        [typeof(MeterValueRecordedV1)] = "charging.meter-value-recorded.v1",
    }.ToFrozenDictionary();

    /// <summary>
    /// None on this branch: <c>charging.meter-value-recorded.v1</c> is read as its own type,
    /// <see cref="MeterValueRecordedV1"/>, and upcast by the journal's read adapter instead.
    /// </summary>
    public static FrozenDictionary<string, Upcaster> Upcasters { get; } =
        FrozenDictionary<string, Upcaster>.Empty;

    /// <summary>Permanent: Ohmline's serializer ids are listed in ADR 0007.</summary>
    public override int Identifier => 9101;

    /// <summary>The closed hierarchies inside Charging's messages, with permanent kinds.</summary>
    private static void AddKinds(JsonSerializerOptions options)
    {
        options.AddClosedHierarchy<SuspendedBy>(
            (typeof(ByEv), "by-ev"),
            (typeof(ByCharger), "by-charger"));
        options.AddClosedHierarchy<TariffComponent>(
            (typeof(EnergyRate), "energy-rate"),
            (typeof(TimeRate), "time-rate"),
            (typeof(SessionFee), "session-fee"),
            (typeof(IdleFee), "idle-fee"));
        options.AddClosedHierarchy<ConnectorSnapshot>(
            (typeof(ConnectorSnapshot.Available), "available"),
            (typeof(ConnectorSnapshot.Reserved), "reserved"),
            (typeof(ConnectorSnapshot.Occupied), "occupied"),
            (typeof(ConnectorSnapshot.Faulted), "faulted"),
            (typeof(ConnectorSnapshot.Unavailable), "unavailable"));
        options.AddClosedHierarchy<SessionPhaseSnapshot>(
            (typeof(SessionPhaseSnapshot.Active), "active"),
            (typeof(SessionPhaseSnapshot.SuspendedByEv), "suspended-by-ev"),
            (typeof(SessionPhaseSnapshot.SuspendedByCharger), "suspended-by-charger"),
            (typeof(SessionPhaseSnapshot.Ended), "ended"));
    }
}
