using System.Collections.Immutable;
using CsCheck;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;
using static Ohmline.SiteEnergy.Domain.Tests.TheDepot;
using Gen = CsCheck.Gen;

namespace Ohmline.SiteEnergy.Domain.Tests.Budgets;

/// <summary>
/// What holds at the depot whatever its sessions and the grid operator do, in any order: random
/// scripts of requests, releases and curtailments, run through Decide and Evolve as the shell
/// will run them, checked after every decision.
/// </summary>
public sealed class SitePowerBudgetProperties
{
    /// <summary>
    /// Any of the six sessions asking for 0 to 60 kW or letting go; a session asking for a
    /// connector another session holds; curtailments from none to more than the site has.
    /// </summary>
    private static readonly Gen<SitePowerBudgetCommand> Commands =
        Gen.OneOf<SitePowerBudgetCommand>(
            Gen.Select(Gen.Int[0, 5], Gen.Int[0, 60_000], (connector, watts) =>
                Request(connector) with { Requested = Power.FromWatts(watts) }),
            Gen.Int[0, 5].Select(connector => Release(connector)),
            Gen.Int[0, 5].Select(connector => Request(connector) with { SessionId = Latecomer }),
            Gen.Int[0, 200].Select(kilowatts =>
                new ApplyCurtailment(Command, Site, new Power(kilowatts))),
            Gen.Const(new EndCurtailment(Command, Site)));

    /// <summary>The depot, with 0 to 6 fleet connectors, then 60 commands at most.</summary>
    private static readonly Gen<ImmutableArray<SitePowerBudgetChecks.Timed>> Scripts =
        Gen.Int[0, 6].SelectMany(fleet => SitePowerBudgetChecks.Scripts(
            Commands, Gen.Const(TimeSpan.FromMinutes(1)), 60, Configure(fleet)));

    [Fact]
    public void Allocations_never_exceed_the_effective_capacity()
    {
        SitePowerBudgetChecks.Invariant(
            Scripts,
            Morning,
            "The allocations fit the capacity and the connectors",
            state => state.Value is not Configured site || site.AllocationsFit);
    }

    [Fact]
    public void Evolve_is_total_for_every_event_decide_produces()
    {
        SitePowerBudgetChecks.EvolveIsTotal(Scripts, Morning);
    }

    [Fact]
    public void Decide_never_records_an_event_that_evolve_ignores()
    {
        SitePowerBudgetChecks.EveryEventChangesTheState(Scripts, Morning);
    }

    [Fact]
    public void Given_allocations_that_break_the_rules_then_the_assertion_is_false()
    {
        var depot = Configured.Of(SiteConfiguration.Create(
            Site,
            new Power(200m),
            new Power(10m),
            "America/Denver",
            Connectors.Select(connector => ConnectorConfiguration.Create(
                connector, new Power(5m), new Power(50m), new Public()))));
        var open = depot with
        {
            Requests = [new OpenRequest(Connectors[0], Sessions[0], new Power(50m))],
        };

        (open with { Allocations = [Allocation(0, 50m)] }).AllocationsFit.ShouldBeTrue();
        (open with { Allocations = [Allocation(0, 60m)] }).AllocationsFit.ShouldBeFalse();
        (open with { Allocations = [Allocation(0, 4m)] }).AllocationsFit.ShouldBeFalse();
        (depot with { Allocations = [Allocation(0, 50m)] }).AllocationsFit.ShouldBeFalse();
        (open with { Curtailment = new Power(160m), Allocations = [Allocation(0, 50m)] })
            .AllocationsFit.ShouldBeFalse();
    }

    private static PowerAllocation Allocation(int connector, decimal kilowatts) =>
        new(Connectors[connector], new Power(kilowatts));
}
