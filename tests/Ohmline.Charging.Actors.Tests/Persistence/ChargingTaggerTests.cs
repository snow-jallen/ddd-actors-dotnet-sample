using Akka.Persistence.Journal;
using Ohmline.Charging.Actors.Persistence;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.Domain.Listings;
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Actors.Tests.Persistence;

public sealed class ChargingTaggerTests
{
    private static readonly ChargingTagger Tagger = new();

    public static TheoryData<object> Events => new(ChargingSamples.All.Where(IsEvent));

    public static TheoryData<object> EverythingElse =>
        new(ChargingSamples.All.Where(message => !IsEvent(message)));

    [Theory]
    [MemberData(nameof(Events))]
    public void Every_event_is_tagged_with_the_one_tag_of_its_family(object @event)
    {
        var tagged = Tagger.ToJournal(@event).ShouldBeOfType<Tagged>();

        tagged.Payload.ShouldBeSameAs(@event);
        tagged.Tags.ShouldHaveSingleItem().ShouldBe(@event switch
        {
            SessionEvent => "charging-session",
            JourneyEvent => "charging-journey",
            _ => "charging",
        });
    }

    [Theory]
    [MemberData(nameof(EverythingElse))]
    public void Anything_that_is_not_an_event_passes_through_untagged(object message) =>
        Tagger.ToJournal(message).ShouldBeSameAs(message);

    [Fact]
    public void The_journal_routes_every_event_family_through_it() =>
        ChargingTagger.BoundTypes.ShouldBe(
            [
                typeof(ChargePointEvent),
                typeof(ChargePointListingEvent),
                typeof(SessionEvent),
                typeof(JourneyEvent),
            ],
            ignoreOrder: true);

    private static bool IsEvent(object message) =>
        message is ChargePointEvent or ChargePointListingEvent or SessionEvent or JourneyEvent;
}
