using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Budgets;

/// <summary>From a site's budget state to its snapshot and back.</summary>
public static class SitePowerBudgetSnapshotMapping
{
    extension(SitePowerBudgetState state)
    {
        /// <summary>Null before the site is configured: there is nothing to keep.</summary>
        public SitePowerBudgetSnapshot? ToSnapshot() => state switch
        {
            Unconfigured => null,
            Configured budget => new(
                budget.Site, budget.Curtailment, budget.Requests, budget.Allocations),
            null => throw new InvalidOperationException("The state is default: it holds no case."),
        };
    }

    extension(SitePowerBudgetSnapshot snapshot)
    {
        public SitePowerBudgetState ToState() => new Configured(
            snapshot.Site, snapshot.Curtailment, snapshot.Requests, snapshot.Allocations);
    }
}
