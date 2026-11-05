using Akka.Persistence.Journal;
using Ohmline.Charging.Actors.Persistence;
using Ohmline.Charging.Actors.Persistence.Legacy;
using Ohmline.Charging.Domain.ChargePoints;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Persistence;

/// <summary>
/// The read adapter as the pure function it is: one stored event in, a sequence of today's
/// events out (section 20.4).
/// </summary>
public sealed class LegacyEventsReadAdapterTests
{
    private static readonly LegacyEventsReadAdapter Adapter = new();

    private static readonly DateTimeOffset Expiry = Morning + ChargePoint.ReservationHold;

    [Fact]
    public void Given_a_reservation_taken_over_then_it_is_read_as_an_expiry_and_a_reservation()
    {
        var by = Command;
        var taken = new ReservationTakenOver(Charger, 1, SomeoneElsesCard, DriversCard, Expiry)
        {
            CommandId = by,
            OccurredAt = Morning,
        };

        var events = Adapter.FromJournal(taken, "charging.reservation-taken-over.v1").Events;

        events.ShouldBe(
        [
            new ReservationExpired(Charger, 1, SomeoneElsesCard)
            {
                CommandId = by,
                OccurredAt = Morning,
            },
            new ConnectorReserved(Charger, 1, DriversCard, Expiry)
            {
                CommandId = by,
                OccurredAt = Morning,
            },
        ]);
    }

    [Fact]
    public void Given_a_heartbeat_the_pilot_stored_then_nothing_is_read()
    {
        var heartbeat = new ChargePointHeartbeatRecorded(Charger);

        Adapter.FromJournal(heartbeat, "charging.charge-point-heartbeat-recorded.v1")
            .ShouldBeSameAs(EventSequence.Empty);
    }

    [Fact]
    public void Given_any_other_event_then_it_is_read_as_it_is()
    {
        var registered = Registered();

        Adapter.FromJournal(registered, "charging.charge-point-registered.v1").Events
            .ShouldBe([registered]);
    }
}
