using Ohmline.SharedKernel.Identity;

namespace Ohmline.SiteEnergy.Contracts.Budgets;

/// <summary>A question for a site's power budget: a family of its own, never a command.</summary>
public closed record SitePowerBudgetQuery(SiteId SiteId);

/// <summary>How is the site's power shared out right now?</summary>
public sealed record GetSitePowerBudget(SiteId SiteId) : SitePowerBudgetQuery(SiteId);
