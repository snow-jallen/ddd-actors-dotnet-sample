using System.Collections.Frozen;
using System.Text.Json;
using Akka.Actor;
using Ohmline.ActorShell.Serialization;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Listings;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Actors.Serialization;

/// <summary>
/// Everything Charging tells, asks, replies or persists, as JSON under a permanent manifest. The
/// identifier and every manifest below are written into the journal beside each event, so they
/// never change and are never reused (ADR 0007). A new type gets a new line; a new shape of an
/// existing type gets a new manifest version, and the old line stays.
/// </summary>
public sealed class ChargingSerializer(ExtendedActorSystem system)
    : ManifestJsonSerializer(system, Manifests, AddKinds)
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
        [typeof(MeterValueRecorded)] = "charging.meter-value-recorded.v1",
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
    }.ToFrozenDictionary();

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
