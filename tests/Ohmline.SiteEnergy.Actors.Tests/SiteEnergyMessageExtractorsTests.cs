using Ohmline.SiteEnergy.Actors.Tests.Serialization;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Actors.Tests;

/// <summary>Every budget command reaches its site's budget; nothing else is routed.</summary>
public sealed class SiteEnergyMessageExtractorsTests
{
    public static TheoryData<string> Samples =>
        new(SiteEnergySamples.All.Select(sample => sample.GetType().Name).Distinct());

    [Theory]
    [MemberData(nameof(Samples))]
    public void Given_a_message_then_it_is_routed_to_its_site_only_if_it_is_a_command(string type)
    {
        var message = SiteEnergySamples.All.First(sample => sample.GetType().Name == type);

        SiteEnergyMessageExtractors.Budgets.EntityId(message)
            .ShouldBe(message is SitePowerBudgetCommand command ? command.SiteId.Value : null);
    }
}
