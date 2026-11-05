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
                Stamped(new CurtailmentApplied(Site, new Power(120m))),
                Stamped(new AllocationsRebalanced(
                    Site,
                    [
                        new PowerAllocation(Fleet1, new Power(50m)),
                        new PowerAllocation(Public2, new Power(20m)),
                    ])));

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
            .ThenAsync(new BudgetCommandAccepted(2), Granted(Fleet1, BusSession));

    [Fact]
    public async Task Given_a_configured_site_in_the_journal_then_the_new_incarnation_remembers() =>
        await Budget.Given(Configured())
            .When(Configure())
            .ThenAsync<BudgetCommandRejected>(
                rejected => rejected.Code.ShouldBe("already-configured"));

    private static SitePowerBudgetEvent Configured() =>
        Stamped(new SiteConfigured(Site, SiteEnergySamples.Depot));

    private static SitePowerBudgetEvent Granted(ConnectorId connector, SessionId session) =>
        Stamped(new AllocationGranted(Site, connector, session, new Power(50m), new Power(50m)));

    private static SitePowerBudgetEvent Stamped(SitePowerBudgetEvent @event) =>
        @event with { CommandId = Command, OccurredAt = Morning };
}
