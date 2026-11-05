using Akka.Persistence.Journal;
using Ohmline.Charging.Actors.Persistence.Legacy;
using Ohmline.Charging.Domain.ChargePoints;

namespace Ohmline.Charging.Actors.Persistence;

/// <summary>
/// Upcasting with a read event adapter (section 20.4): the journal deserializes each stored row,
/// then hands this adapter whatever came out of it, before the actor sees anything. Unlike the
/// serializer, which reads one object per row, an adapter returns a sequence: it can split one
/// old event into several of today's, or drop one with <see cref="EventSequence.Empty"/>.
/// </summary>
/// <remarks>
/// Every event it gives back carries the old event's command and time, and replays under the
/// old row's sequence number: the journal still holds one row.
/// </remarks>
public sealed class LegacyEventsReadAdapter : IReadEventAdapter
{
    /// <summary>The adapter's name in the journal's configuration.</summary>
    public const string Name = "charging-legacy-events";

    /// <summary>The journal passes events of these types through this adapter.</summary>
    public static IReadOnlyList<Type> BoundTypes { get; } = [typeof(LegacyChargingEvent)];

    public IEventSequence FromJournal(object evt, string manifest) => evt switch
    {
        ReservationTakenOver taken => EventSequence.Create(
            Stamped(
                new ReservationExpired(
                    taken.ChargePointId, taken.ConnectorNumber, taken.PreviousTokenUid),
                taken),
            Stamped(
                new ConnectorReserved(
                    taken.ChargePointId, taken.ConnectorNumber, taken.TokenUid, taken.ExpiresAt),
                taken)),
        ChargePointHeartbeatRecorded => EventSequence.Empty,
        _ => EventSequence.Single(evt),
    };

    private static ChargePointEvent Stamped(ChargePointEvent @event, LegacyChargingEvent from) =>
        @event with { CommandId = from.CommandId, OccurredAt = from.OccurredAt };
}
