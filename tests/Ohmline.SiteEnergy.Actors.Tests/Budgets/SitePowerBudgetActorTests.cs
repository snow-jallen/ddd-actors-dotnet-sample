using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Actors.Budgets;
using Ohmline.SiteEnergy.Actors.Tests.Serialization;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;
using Ohmline.Testing;
using static Ohmline.SiteEnergy.Actors.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Actors.Tests.Budgets;

/// <summary>
/// The budget's shell, on the same DeciderActor as Charging's aggregates. The allocator and the
/// invariant are the decider's tests; here, the replies, the versions and the journal.
/// </summary>
public sealed class SitePowerBudgetActorTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(Serializers.SiteEnergy), nameof(SitePowerBudgetActorTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private PersistenceShellSpec.Scenario Budget =>
        Shell(SitePowerBudgetActor.Props(Site, _clock), SitePowerBudgetActor.PersistenceIdOf(Site));

    [Fact]
    public void The_persistence_id_is_the_durable_type_name_and_the_site() =>
        SitePowerBudgetActor.PersistenceIdOf(Site).ShouldBe("site-power-budget-slc-depot");

    [Fact]
    public async Task Given_a_busy_site_when_the_grid_curtails_then_versions_count_every_event() =>
        // Curtailment applied, and the allocations rebalanced: two events in one decision.
        await Budget.Given(Configured(), Granted(Fleet1, BusSession), Granted(Public2, CarSession))
            .When(new ApplyCurtailment(Command, Site, new Power(120m)))
            .ThenAsync(
                new BudgetCommandAccepted(5),
                Stamped(new CurtailmentApplied(Site, new Power(120m)), by: Command),
                Stamped(
                    new AllocationsRebalanced(
                        Site,
                        [
                            new PowerAllocation(Fleet1, new Power(50m)),
                            new PowerAllocation(Public2, new Power(20m)),
                        ]),
                    by: Command));

    [Fact]
    public async Task Given_an_open_request_when_another_session_asks_then_it_is_rejected() =>
        await Budget.Given(Configured(), Granted(Fleet1, BusSession))
            .When(Request(Fleet1, CarSession))
            .ThenAsync<BudgetCommandRejected>(
                rejected => rejected.Code.ShouldBe("connector-has-open-request"));

    [Fact]
    public async Task Given_an_unconfigured_site_when_power_is_requested_then_it_is_rejected() =>
        await Budget.Given()
            .When(Request(Fleet1, BusSession))
            .ThenAsync<BudgetCommandRejected>(
                rejected => rejected.Code.ShouldBe("not-configured"));

    [Fact]
    public async Task Given_a_configured_site_when_power_is_requested_then_it_is_granted() =>
        await Budget.Given(Configured())
            .When(Request(Fleet1, BusSession))
            .ThenAsync(
                new PowerAllocated(Site, Fleet1, BusSession, new Power(50m), Version: 2),
                Granted(Fleet1, BusSession, by: Command));

    [Fact]
    public async Task Given_a_site_with_no_room_when_power_is_requested_then_it_is_queued() =>
        await Budget.Given(
                Configured(),
                Stamped(new CurtailmentApplied(Site, new Power(190m))))
            .When(Request(Fleet1, BusSession))
            .ThenAsync(
                new QueuedForPower(Site, Fleet1, BusSession, Version: 3),
                Stamped(
                    new AllocationQueued(Site, Fleet1, BusSession, new Power(50m)), by: Command));

    [Fact]
    public async Task Given_a_granted_request_when_it_is_asked_again_then_it_hears_its_allocation()
    {
        var again = Request(Fleet1, BusSession) with
        {
            CommandId = new CommandId(Guid.CreateVersion7(Morning.AddMinutes(1))),
        };

        await Budget.Given(Configured(), Granted(Fleet1, BusSession))
            .When(again)
            .ThenAsync(new PowerAllocated(Site, Fleet1, BusSession, new Power(50m), Version: 2));
    }

    [Fact]
    public async Task Given_a_configured_site_in_the_journal_then_the_new_incarnation_remembers() =>
        await Budget.Given(Configured())
            .When(Configure())
            .ThenAsync<BudgetCommandRejected>(
                rejected => rejected.Code.ShouldBe("already-configured"));

    [Fact]
    public async Task Given_a_curtailed_site_when_asked_then_it_shows_each_request_and_power() =>
        await Budget.Given(
                Configured(),
                Granted(Fleet1, BusSession),
                Granted(Public2, CarSession),
                Stamped(new CurtailmentApplied(Site, new Power(120m))),
                Stamped(new AllocationsRebalanced(
                    Site,
                    [
                        new PowerAllocation(Fleet1, new Power(50m)),
                        new PowerAllocation(Public2, new Power(20m)),
                    ])))
            .When(new GetSitePowerBudget(Site))
            .ThenAsync(new SitePowerBudgetView(
                Site,
                Version: 5,
                EffectiveCapacity: new Power(70m),
                Curtailment: new Power(120m),
                [
                    new RequestView(Fleet1, BusSession, new Power(50m), new Power(50m)),
                    new RequestView(Public2, CarSession, new Power(50m), new Power(20m)),
                ]));

    [Fact]
    public async Task Given_an_unconfigured_site_when_asked_then_it_has_version_0_and_no_power() =>
        await Budget.Given()
            .When(new GetSitePowerBudget(Site))
            .ThenAsync(new SitePowerBudgetView(Site, 0, Power.Zero, Power.Zero, []));

    private static SitePowerBudgetEvent Configured() =>
        Stamped(new SiteConfigured(Site, SiteEnergySamples.Depot));

    private static SitePowerBudgetEvent Granted(
        ConnectorId connector, SessionId session, CommandId? by = null) =>
        Stamped(
            new AllocationGranted(Site, connector, session, new Power(50m), new Power(50m)), by);

    /// <summary>
    /// Stamped by the command with the id <paramref name="by"/>, or, in a history, by an earlier
    /// command of its own: the shell answers a command whose id it has seen without deciding it.
    /// </summary>
    private static SitePowerBudgetEvent Stamped(
        SitePowerBudgetEvent @event, CommandId? by = null) =>
        @event with { CommandId = by ?? new(Guid.CreateVersion7(Morning)), OccurredAt = Morning };
}
