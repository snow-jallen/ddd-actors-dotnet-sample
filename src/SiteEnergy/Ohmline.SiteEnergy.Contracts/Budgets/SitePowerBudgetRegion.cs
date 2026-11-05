namespace Ohmline.SiteEnergy.Contracts.Budgets;

/// <summary>
/// The key under which a node's actor registry holds the site power budget shard region
/// (<c>site-power-budget</c>): code that sends budget commands asks for
/// <c>IRequiredActor&lt;SitePowerBudgetRegion&gt;</c> and needs no reference to the actors.
/// </summary>
public sealed class SitePowerBudgetRegion;
