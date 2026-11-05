using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Persistence.Legacy;

/// <summary>
/// Events in shapes Charging's model no longer has, as a pilot at the bus depot stored them
/// before today's events existed: a demonstration of what a read event adapter is for (section
/// 20.4). They are read from the journal, never written by anything in Charging, and never seen
/// by a decider: <see cref="LegacyEventsReadAdapter"/> turns each into today's events, or none.
/// </summary>
public closed record LegacyChargingEvent(ChargePointId ChargePointId)
{
    public CommandId CommandId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// The pilot recorded a lapsed reservation taken over by another driver as one event. Charging
/// records two facts for it: the old reservation expired, and a new one began.
/// </summary>
public sealed record ReservationTakenOver(
    ChargePointId ChargePointId,
    int ConnectorNumber,
    TokenUid PreviousTokenUid,
    TokenUid TokenUid,
    DateTimeOffset ExpiresAt)
    : LegacyChargingEvent(ChargePointId);

/// <summary>
/// The pilot stored every heartbeat as an event. A heartbeat is liveness, not a decision, and
/// Charging keeps none: the adapter drops it.
/// </summary>
public sealed record ChargePointHeartbeatRecorded(ChargePointId ChargePointId)
    : LegacyChargingEvent(ChargePointId);
