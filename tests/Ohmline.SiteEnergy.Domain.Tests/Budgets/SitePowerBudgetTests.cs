using System.Collections.Immutable;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;
using static Ohmline.SiteEnergy.Domain.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Domain.Tests.Budgets;

/// <summary>
/// The site power budget's decision table, mostly at the primer's depot, as Given-When-Then. The
/// history is built by deciding commands one by one, as the shell will, and recording their
/// events.
/// </summary>
public sealed class SitePowerBudgetTests
{
    // ConfigureSite: the factory

    [Fact]
    public void Given_an_unconfigured_site_when_configured_then_the_checked_values_are_recorded()
    {
        Given()
            .When(Configure())
            .Then(new SiteConfigured(Site, DepotConfiguration()).At(Morning));
    }

    [Fact]
    public void Given_an_unconfigured_site_when_the_margin_swallows_the_limit_then_it_is_invalid()
    {
        var configure = Configure() with { GridLimit = new Power(60m), SafetyMargin = new(60m) };

        Given()
            .When(configure)
            .ThenRejects(
                (ValidationError)new OutOfRange(
                    "Safety margin", 60m, "below the grid limit of 60 kW"));
    }

    [Fact]
    public void Given_an_unconfigured_site_when_a_connector_is_invalid_then_its_error_comes_back()
    {
        var configure = Configure();
        configure = configure with
        {
            Connectors = configure.Connectors.SetItem(
                0, configure.Connectors[0] with { MinPower = new Power(60m) }),
        };

        Given()
            .When(configure)
            .ThenRejects((ValidationError)new OutOfRange("Minimum power", 60m, "at most 50 kW"));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("release")]
    [InlineData("curtail")]
    [InlineData("end curtailment")]
    public void Given_an_unconfigured_site_when_anything_else_arrives_then_it_is_not_configured(
        string command)
    {
        SitePowerBudgetCommand early = command switch
        {
            "request" => Request(0),
            "release" => Release(0),
            "curtail" => new ApplyCurtailment(Command, Site, new Power(40m)),
            "end curtailment" => new EndCurtailment(Command, Site),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        Given()
            .When(early)
            .ThenRejects(new SitePowerBudgetError.NotConfigured(Site));
    }

    [Fact]
    public void Given_a_configured_site_when_configured_again_then_it_is_already_configured()
    {
        Given(Configure())
            .When(Configure())
            .ThenRejects(new SitePowerBudgetError.AlreadyConfigured(Site));
    }

    // RequestAllocation

    [Fact]
    public void Given_a_site_with_room_when_a_session_asks_for_power_then_it_is_granted_in_full()
    {
        Given(Configure())
            .When(Request(0))
            .Then(Granted(0, requested: 50m, granted: 50m).At(Morning));
    }

    [Fact]
    public void Given_four_cars_charging_when_a_fifth_asks_for_50_kW_then_all_five_get_38_kW()
    {
        Given(Configure(), Request(0), Request(1), Request(2), Request(3))
            .When(Request(4))
            .Then(
                Granted(4, requested: 50m, granted: 38m).At(Morning),
                Rebalanced(38m, 38m, 38m, 38m, 38m).At(Morning));
    }

    [Fact]
    public void Given_a_request_below_the_connectors_minimum_then_it_is_granted_the_minimum()
    {
        Given(Configure())
            .When(Request(0, kilowatts: 3m))
            .Then(Granted(0, requested: 3m, granted: 5m).At(Morning));
    }

    [Fact]
    public void Given_no_room_for_another_minimum_when_a_session_asks_then_it_is_queued()
    {
        Given(Configure(), Curtail(170m), Request(0), Request(1), Request(2), Request(3))
            .When(Request(4))
            .Then(
                new AllocationQueued(Site, Connectors[4], Sessions[4], new Power(50m)).At(Morning));
    }

    [Fact]
    public void Given_a_fleet_request_when_the_site_is_full_then_a_public_session_is_queued_again()
    {
        Given(Configure(fleet: 1), Curtail(170m), Request(1), Request(2), Request(3), Request(4))
            .When(Request(0))
            .Then(
                Granted(0, requested: 50m, granted: 5m).At(Morning),
                new AllocationsRebalanced(Site, [.. Allocations(1..4, 5m), Allocation(0, 5m)])
                    .At(Morning));
    }

    [Fact]
    public void Given_a_connector_from_another_site_when_power_is_requested_then_it_is_unknown()
    {
        var elsewhere = new ConnectorId(new ChargePointId("OHM-SLC-0099"), 1);

        Given(Configure())
            .When(Request(0) with { ConnectorId = elsewhere })
            .ThenRejects(new SitePowerBudgetError.UnknownConnector(elsewhere));
    }

    [Fact]
    public void Given_an_open_request_when_the_same_session_asks_again_then_nothing_changes()
    {
        Given(Configure(), Request(0))
            .When(Request(0))
            .ThenNothingChanges();
    }

    [Fact]
    public void Given_an_open_request_when_another_session_wants_its_connector_then_it_is_refused()
    {
        Given(Configure(), Request(0))
            .When(Request(0) with { SessionId = Latecomer })
            .ThenRejects(
                new SitePowerBudgetError.ConnectorHasOpenRequest(Connectors[0], Sessions[0]));
    }

    // ReleaseAllocation

    [Fact]
    public void Given_five_cars_charging_when_one_leaves_then_the_other_four_share_its_power()
    {
        Given(Configure(), Request(0), Request(1), Request(2), Request(3), Request(4))
            .When(Release(0))
            .Then(
                new AllocationReleased(Site, Connectors[0], Sessions[0]).At(Morning),
                new AllocationsRebalanced(Site, Allocations(1..5, 47.5m)).At(Morning));
    }

    [Fact]
    public void Given_a_queued_request_when_another_session_leaves_then_the_rebalance_admits_it()
    {
        Given(Configure(), Curtail(170m), Request(0), Request(1), Request(2), Request(3),
                Request(4))
            .When(Release(0))
            .Then(
                new AllocationReleased(Site, Connectors[0], Sessions[0]).At(Morning),
                new AllocationsRebalanced(Site, Allocations(1..5, 5m)).At(Morning));
    }

    [Fact]
    public void Given_no_open_request_for_the_session_when_released_then_nothing_changes()
    {
        Given(Configure())
            .When(Release(0))
            .ThenNothingChanges();
        Given(Configure(), Request(0))
            .When(Release(0) with { SessionId = Latecomer })
            .ThenNothingChanges();
    }

    // ApplyCurtailment and EndCurtailment

    [Fact]
    public void Given_five_cars_when_the_grid_asks_for_40_kW_less_then_each_drops_to_30_kW()
    {
        Given(Configure(), Request(0), Request(1), Request(2), Request(3), Request(4))
            .When(Curtail(40m))
            .Then(
                new CurtailmentApplied(Site, new Power(40m)).At(Morning),
                Rebalanced(30m, 30m, 30m, 30m, 30m).At(Morning));
    }

    [Fact]
    public void Given_a_curtailment_in_force_when_the_same_one_arrives_again_then_nothing_changes()
    {
        Given(Configure(), Curtail(40m))
            .When(Curtail(40m))
            .ThenNothingChanges();
    }

    [Fact]
    public void Given_a_curtailment_deeper_than_the_minimums_then_the_last_session_is_queued_again()
    {
        Given(Configure(), Request(0), Request(1), Request(2), Request(3), Request(4))
            .When(Curtail(170m))
            .Then(
                new CurtailmentApplied(Site, new Power(170m)).At(Morning),
                new AllocationsRebalanced(Site, Allocations(0..4, 5m)).At(Morning));
    }

    [Fact]
    public void Given_a_curtailment_when_it_ends_then_the_allocations_rise_again()
    {
        Given(Configure(), Request(0), Request(1), Request(2), Request(3), Request(4),
                Curtail(40m))
            .When(new EndCurtailment(Command, Site))
            .Then(
                new CurtailmentEnded(Site).At(Morning),
                Rebalanced(38m, 38m, 38m, 38m, 38m).At(Morning));
    }

    [Fact]
    public void Given_no_curtailment_when_one_ends_then_nothing_changes()
    {
        Given(Configure())
            .When(new EndCurtailment(Command, Site))
            .ThenNothingChanges();
    }

    // Causation

    [Fact]
    public void Given_a_request_that_moves_the_others_then_every_event_names_it_as_the_cause()
    {
        var nine = Morning.AddHours(1);
        var request = Request(4) with { CommandId = new CommandId(Guid.CreateVersion7(nine)) };

        Given(Configure(), Request(0), Request(1), Request(2), Request(3))
            .At(nine)
            .When(request)
            .Then(
                CausedByTheRequest(Granted(4, requested: 50m, granted: 38m)),
                CausedByTheRequest(Rebalanced(38m, 38m, 38m, 38m, 38m)));

        SitePowerBudgetEvent CausedByTheRequest(SitePowerBudgetEvent @event) =>
            @event with { CommandId = request.CommandId, OccurredAt = nine };
    }

    // The invariant, Evolve and Fold

    [Fact]
    public void Given_a_busy_morning_then_after_every_decision_the_allocations_fit_the_capacity()
    {
        SitePowerBudgetCommand[] morning =
        [
            Configure(fleet: 2), Request(2), Request(3), Request(0, 22m), Curtail(60m),
            Request(4), Request(1), Request(5, 11m), Curtail(150m), Release(2), Request(2),
            new EndCurtailment(Command, Site), Release(0), Curtail(185m), Release(4), Release(3),
            new EndCurtailment(Command, Site),
        ];
        var state = SitePowerBudget.Initial;

        foreach (var command in morning)
        {
            state = Handle(state, command);
            var site = state.Value.ShouldBeOfType<Configured>();
            var limits = site.AllocationRequests.ToDictionary(request => request.ConnectorId);

            (site.Allocated <= site.EffectiveCapacity).ShouldBeTrue();
            site.Allocations.ShouldAllBe(allocation =>
                allocation.Power >= limits[allocation.ConnectorId].MinPower
                && allocation.Power <= limits[allocation.ConnectorId].MaxPower);
        }
    }

    [Fact]
    public void Given_one_event_of_every_type_then_the_tests_cover_the_whole_hierarchy()
    {
        var everyType = typeof(SitePowerBudgetEvent).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SitePowerBudgetEvent)) && !type.IsAbstract);

        OneOfEach.Select(@event => @event.GetType()).Distinct()
            .ShouldBe(everyType, ignoreOrder: true);
    }

    [Fact]
    public void Given_one_event_of_every_type_when_folded_then_the_recorded_allocations_are_kept()
    {
        var site = SitePowerBudget.Fold(OneOfEach).Value.ShouldBeOfType<Configured>();

        site.Curtailment.ShouldBe(Power.Zero);
        site.Requests.Select(request => request.ConnectorId).ShouldBe([Connectors[1]]);
        site.Allocations.ShouldBe([Allocation(1, 41m)]);
    }

    [Fact]
    public void Given_events_that_decide_never_produces_when_evolved_then_nothing_fails()
    {
        var configured = SitePowerBudget.Fold(Journal(Configure()));

        SitePowerBudget.Evolve(SitePowerBudget.Initial, Granted(0, 50m, 50m))
            .ShouldBe(SitePowerBudget.Initial);
        SitePowerBudget.Evolve(configured, new SiteConfigured(Site, DepotConfiguration()))
            .ShouldBe(configured);
        SitePowerBudget.Evolve(configured, new AllocationReleased(Site, Connectors[3], Latecomer))
            .ShouldBe(configured);
    }

    [Fact]
    public void Given_a_default_state_when_deciding_then_the_null_arm_reports_the_bug()
    {
        Should.Throw<InvalidOperationException>(
            () => SitePowerBudget.Decide(default, Request(0), new DecisionContext(Morning)));
    }

    private static readonly ImmutableArray<SitePowerBudgetEvent> OneOfEach =
    [
        new SiteConfigured(Site, DepotConfiguration()),
        Granted(0, requested: 50m, granted: 50m),
        new AllocationQueued(Site, Connectors[1], Sessions[1], new Power(50m)),
        new CurtailmentApplied(Site, new Power(140m)),
        Rebalanced(25m, 25m),
        new AllocationReleased(Site, Connectors[0], Sessions[0]),
        new AllocationsRebalanced(Site, [Allocation(1, 41m)]),
        new CurtailmentEnded(Site),
    ];

    /// <summary>
    /// A site whose history is what these commands recorded, decided one after another, as the
    /// shell will decide them. The allocations in it come from the allocator, so no test repeats
    /// the allocator's arithmetic to write them down.
    /// </summary>
    private static SitePowerBudgetSpec Given(params SitePowerBudgetCommand[] commands) =>
        SitePowerBudgetSpec.Given(Journal(commands)).At(Morning);

    private static ImmutableArray<SitePowerBudgetEvent> Journal(
        params SitePowerBudgetCommand[] commands) =>
        commands.Aggregate(
            ImmutableArray<SitePowerBudgetEvent>.Empty,
            (journal, command) =>
                journal.AddRange(Decided(SitePowerBudget.Fold(journal), command)));

    /// <summary>Decides one command that must be accepted, and evolves by its events.</summary>
    private static SitePowerBudgetState Handle(
        SitePowerBudgetState state, SitePowerBudgetCommand command) =>
        Decider.Fold<SitePowerBudget, SitePowerBudgetState, SitePowerBudgetCommand,
            SitePowerBudgetEvent, SitePowerBudgetError>(state, Decided(state, command));

    private static ImmutableArray<SitePowerBudgetEvent> Decided(
        SitePowerBudgetState state, SitePowerBudgetCommand command) =>
        SitePowerBudget.Decide(state, command, new DecisionContext(Morning)).Value
            .ShouldBeOfType<Ok<ImmutableArray<SitePowerBudgetEvent>>>().Value;

    private static ApplyCurtailment Curtail(decimal kilowatts) =>
        new(Command, Site, new Power(kilowatts));

    private static AllocationGranted Granted(int index, decimal requested, decimal granted) =>
        new(Site, Connectors[index], Sessions[index], new Power(requested), new Power(granted));

    private static AllocationsRebalanced Rebalanced(params decimal[] kilowatts) =>
        new(Site, [.. kilowatts.Index().Select(entry => Allocation(entry.Index, entry.Item))]);

    private static ImmutableArray<PowerAllocation> Allocations(Range connectors, decimal kw) =>
        [.. Connectors[connectors].Select(connector => new PowerAllocation(
            connector, new Power(kw)))];

    private static PowerAllocation Allocation(int index, decimal kilowatts) =>
        new(Connectors[index], new Power(kilowatts));

    private static SiteConfiguration DepotConfiguration() => SiteConfiguration.Create(
        Site,
        new Power(200m),
        new Power(10m),
        "America/Denver",
        Connectors.Select(connector => ConnectorConfiguration.Create(
            connector, new Power(5m), new Power(50m), new Public())));
}
