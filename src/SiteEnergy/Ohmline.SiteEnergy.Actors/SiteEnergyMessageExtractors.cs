using Ohmline.ActorShell.Sharding;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Actors;

/// <summary>Which site power budget each of Site Energy's messages is for: its site.</summary>
public static class SiteEnergyMessageExtractors
{
    public static EntityMessageExtractor Budgets { get; } = new(message => message switch
    {
        SitePowerBudgetCommand command => command.SiteId.Value,
        _ => null,
    });
}
