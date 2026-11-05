using Ohmline.SiteEnergy.Actors.Tests.Serialization;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Actors.Tests;

/// <summary>Every budget command and question reaches its site's budget; nothing else.</summary>
public sealed class SiteEnergyMessageExtractorsTests
{
    public static TheoryData<string> Samples =>
        new(SiteEnergySamples.All.Select(sample => sample.GetType().Name).Distinct());

    [Theory]
    [MemberData(nameof(Samples))]
    public void Given_a_message_then_only_commands_and_questions_reach_the_site(string type)
    {
        var message = SiteEnergySamples.All.First(sample => sample.GetType().Name == type);

        SiteEnergyMessageExtractors.Budgets.EntityId(message).ShouldBe(message switch
        {
            SitePowerBudgetCommand command => command.SiteId.Value,
            SitePowerBudgetQuery query => query.SiteId.Value,
            _ => null,
        });
    }
}
