using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;
using static Ohmline.SiteEnergy.Domain.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Domain.Tests.Budgets;

/// <summary>
/// What Site Energy's allocation events show other contexts, through the published
/// <see cref="ISiteAllocationEvent"/> (section 13.8).
/// </summary>
public sealed class SiteAllocationEventTests
{
    [Fact]
    public void A_grant_shows_the_power_granted_to_its_connector() =>
        ShouldShow(
            new AllocationGranted(Site, Connectors[0], Sessions[0], new(50m), new(32m)),
            coversWholeSite: false,
            new ConnectorAllocation(Connectors[0], new Power(32m)));

    [Fact]
    public void A_queued_request_shows_nothing_allocated_yet() =>
        ShouldShow(
            new AllocationQueued(Site, Connectors[1], Sessions[1], new(50m)),
            coversWholeSite: false,
            new ConnectorAllocation(Connectors[1], Power.Zero));

    [Fact]
    public void A_rebalance_shows_every_allocation_at_the_site() =>
        ShouldShow(
            new AllocationsRebalanced(
                Site,
                [new PowerAllocation(Connectors[0], new(30m)), new(Connectors[2], new(20m))]),
            coversWholeSite: true,
            new ConnectorAllocation(Connectors[0], new Power(30m)),
            new ConnectorAllocation(Connectors[2], new Power(20m)));

    [Fact]
    public void A_release_shows_the_connector_with_no_allocation() =>
        ShouldShow(
            new AllocationReleased(Site, Connectors[0], Sessions[0]),
            coversWholeSite: false,
            new ConnectorAllocation(Connectors[0], Allocated: null));

    [Fact]
    public void Configuration_and_curtailment_allocate_nothing_by_themselves()
    {
        var configuration = SiteConfiguration.Create(
            Site,
            new Power(200m),
            new Power(10m),
            "America/Denver",
            [ConnectorConfiguration.Create(Connectors[0], new(5m), new(50m), new Public())]);

        new SiteConfigured(Site, configuration).ShouldNotBeAssignableTo<ISiteAllocationEvent>();
        new CurtailmentApplied(Site, new(60m)).ShouldNotBeAssignableTo<ISiteAllocationEvent>();
        new CurtailmentEnded(Site).ShouldNotBeAssignableTo<ISiteAllocationEvent>();
    }

    private static void ShouldShow(
        ISiteAllocationEvent @event, bool coversWholeSite, params ConnectorAllocation[] expected)
    {
        @event.SiteId.ShouldBe(Site);
        @event.CoversWholeSite.ShouldBe(coversWholeSite);
        @event.Allocations.ShouldBe(expected);
    }
}
