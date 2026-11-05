using Akka.Persistence.Journal;
using Ohmline.SiteEnergy.Actors.Persistence;
using Ohmline.SiteEnergy.Actors.Tests.Serialization;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Tests.Persistence;

public sealed class SiteEnergyTaggerTests
{
    private static readonly SiteEnergyTagger Tagger = new();

    public static TheoryData<object> Events =>
        new(SiteEnergySamples.All.Where(message => message is SitePowerBudgetEvent));

    public static TheoryData<object> EverythingElse =>
        new(SiteEnergySamples.All.Where(message => message is not SitePowerBudgetEvent));

    [Theory]
    [MemberData(nameof(Events))]
    public void Every_event_is_tagged_site_energy(object @event)
    {
        var tagged = Tagger.ToJournal(@event).ShouldBeOfType<Tagged>();

        tagged.Payload.ShouldBeSameAs(@event);
        tagged.Tags.ShouldHaveSingleItem().ShouldBe("site-energy");
    }

    [Theory]
    [MemberData(nameof(EverythingElse))]
    public void Anything_that_is_not_an_event_passes_through_untagged(object message) =>
        Tagger.ToJournal(message).ShouldBeSameAs(message);
}
