using Akka.Actor;
using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Actors.Budgets;
using Ohmline.SiteEnergy.Actors.Diagnostics;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;
using Ohmline.Testing.Metrics;
using static Ohmline.SiteEnergy.Actors.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Actors.Tests.Diagnostics;

/// <summary>
/// The allocation gauge (section 18.1): read when it is collected, from the figures the budget
/// actors left behind, with no tag at all.
/// </summary>
public sealed class SiteEnergyMetricsTests : PersistenceShellSpec
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    /// <summary>The depot's 200 kW connection, less its 10 kW safety margin.</summary>
    private const double DepotCapacity = 190.0;

    private readonly FakeTimeProvider _clock = new(Morning);
    private readonly TestMeterFactory _meters = new();
    private readonly InstrumentRecorder _recorded;
    private readonly SiteEnergyMetrics _metrics;
    private int _commands;

    public SiteEnergyMetricsTests(ITestOutputHelper output)
        : base(
            ActorTestConfig.With(Serializers.SiteEnergy), nameof(SiteEnergyMetricsTests), output)
    {
        _recorded = new InstrumentRecorder(_meters);
        _metrics = new SiteEnergyMetrics(_meters);
    }

    [Fact]
    public void Given_no_budget_yet_then_the_gauge_reports_nothing()
    {
        _recorded.Observe();

        _recorded.Of(SiteEnergyMetrics.AllocationUtilization).ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_power_allocated_at_two_sites_then_the_gauge_reports_the_fuller_one()
    {
        var depot = Sys.ActorOf(SitePowerBudgetActor.Props(Site, _clock, _metrics));
        await AskAsync<BudgetCommandAccepted>(depot, Configure());
        await AskAsync<PowerAllocated>(depot, Request(Fleet1, BusSession));

        var yard = new SiteId("slc-yard");
        var other = Sys.ActorOf(SitePowerBudgetActor.Props(yard, _clock, _metrics));
        await AskAsync<BudgetCommandAccepted>(other, Configure() with { SiteId = yard });
        await AskAsync<PowerAllocated>(other, Request(Fleet1, BusSession) with { SiteId = yard });
        await AskAsync<PowerAllocated>(other, Request(Public2, CarSession) with { SiteId = yard });

        var utilization = Observed();
        utilization.Value.ShouldBe(100 / DepotCapacity, tolerance: 1e-9);
        utilization.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_curtailment_that_leaves_no_headroom_then_the_site_is_full()
    {
        var depot = Sys.ActorOf(SitePowerBudgetActor.Props(Site, _clock, _metrics));
        await AskAsync<BudgetCommandAccepted>(depot, Configure());
        await AskAsync<PowerAllocated>(depot, Request(Fleet1, BusSession));

        await AskAsync<BudgetCommandAccepted>(
            depot, new ApplyCurtailment(NextCommand(), Site, new Power(140m)));

        Observed().Value.ShouldBe(1.0);
    }

    [Fact]
    public async Task Given_a_budget_that_recovers_then_the_gauge_has_its_figure_at_once()
    {
        var first = Sys.ActorOf(SitePowerBudgetActor.Props(Site, _clock));
        await AskAsync<BudgetCommandAccepted>(first, Configure());
        await AskAsync<PowerAllocated>(first, Request(Fleet1, BusSession));
        await WatchAsync(first);
        Sys.Stop(first);
        await ExpectTerminatedAsync(first, cancellationToken: Ct);

        var recovered = Sys.ActorOf(SitePowerBudgetActor.Props(Site, _clock, _metrics));
        await recovered.Ask<SitePowerBudgetView>(new GetSitePowerBudget(Site), Patience, Ct);

        Observed().Value.ShouldBe(50 / DepotCapacity, tolerance: 1e-9);
    }

    private InstrumentRecorder.Recorded Observed()
    {
        _recorded.Observe();
        return _recorded.Of(SiteEnergyMetrics.AllocationUtilization).ShouldHaveSingleItem();
    }

    /// <summary>Each command has an id of its own, or the shell takes it for a duplicate.</summary>
    private Task<TReply> AskAsync<TReply>(IActorRef budget, SitePowerBudgetCommand command) =>
        budget.Ask<TReply>(command with { CommandId = NextCommand() }, Patience, Ct);

    private CommandId NextCommand() =>
        new(Guid.CreateVersion7(Morning.AddSeconds(Interlocked.Increment(ref _commands))));
}
