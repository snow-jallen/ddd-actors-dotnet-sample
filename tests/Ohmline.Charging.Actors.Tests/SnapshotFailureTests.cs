using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Listings;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// A snapshot is a cache: when one cannot be saved, the shell logs a WARNING and nothing else
/// changes. The reply is the same, and the events stay, even for a state-sourced aggregate.
/// </summary>
public sealed class SnapshotFailureTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(Serializers.Charging), nameof(SnapshotFailureTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    [Fact]
    public async Task Given_a_snapshot_due_that_cannot_be_saved_then_the_reply_is_unchanged()
    {
        var scenario = Shell(
                ChargePointActor.Props(Charger, _clock), ChargePointActor.PersistenceIdOf(Charger))
            .Given(NinetyNineEvents())
            .When(Reserve(1, DriversCard))
            .WithSnapshotSave(save => save.Fail());

        await EventFilter
            .Warning(contains:
                "charge-point-OHM-SLC-0042 could not save its snapshot at sequence number 100")
            .ExpectOneAsync(
                () => scenario.ThenAsync(new CommandAccepted(100), Reserved(1, DriversCard)),
                Ct);
    }

    [Fact]
    public async Task Given_a_state_sourced_listing_whose_snapshot_fails_then_its_event_stays()
    {
        var scenario = Shell(
                ChargePointListingActor.Props(Charger, _clock),
                ChargePointListingActor.PersistenceIdOf(Charger))
            .Given()
            .When(new ReviseChargePointListing(Command, Charger, "Depot", "Level -2", "24/7"))
            .WithSnapshotSave(save => save.Fail());

        await EventFilter
            .Warning(contains: "could not save its snapshot at sequence number 1")
            .ExpectOneAsync(
                () => scenario.ThenAsync(
                    new CommandAccepted(1),
                    new ChargePointListingRevised(Charger, "Depot", "Level -2", "24/7", 1)
                    {
                        CommandId = Command,
                        OccurredAt = Morning,
                    }),
                Ct);
    }

    /// <summary>A registration, then a reservation and its cancellation, over and over.</summary>
    private static IEnumerable<object> NinetyNineEvents() =>
    [
        Registered(),
        .. Enumerable.Range(2, 98).Select(sequenceNr => sequenceNr % 2 == 0
            ? Reserved(1, DriversCard)
            : Stamped(new ReservationCancelled(Charger, 1, DriversCard))),
    ];
}
