using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Budgets;

/// <summary>From the budget's state to the plain view that answers a question about it.</summary>
public static class BudgetViewMapping
{
    extension(SitePowerBudgetState state)
    {
        public SitePowerBudgetView ToView(SiteId siteId, long version) => state switch
        {
            Unconfigured => new(siteId, version, default, default, []),
            Configured site => new(
                siteId,
                version,
                site.EffectiveCapacity,
                site.Curtailment,
                [
                    .. site.Requests.Select(request => new RequestView(
                        request.ConnectorId,
                        request.SessionId,
                        request.Requested,
                        site.Allocations
                            .Where(allocation => allocation.ConnectorId == request.ConnectorId)
                            .Select(allocation => (Power?)allocation.Power)
                            .FirstOrDefault())),
                ]),
            null => throw new InvalidOperationException(
                "This SitePowerBudgetState is default: it holds no case."),
        };
    }
}
