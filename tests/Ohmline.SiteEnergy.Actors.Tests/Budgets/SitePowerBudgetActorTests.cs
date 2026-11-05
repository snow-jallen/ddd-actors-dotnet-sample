using Akka.Actor;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.SiteEnergy.Actors.Budgets;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;
using static Ohmline.SiteEnergy.Actors.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Actors.Tests.Budgets;

/// <summary>
/// The budget's shell, on the same DeciderActor as Charging's aggregates. The allocator and the
/// invariant are the decider's tests; here, the replies, the versions and recovery.
/// </summary>
public sealed class SitePowerBudgetActorTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.SiteEnergy), nameof(SitePowerBudgetActorTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_persistence_id_is_the_durable_type_name_and_the_site() =>
        SitePowerBudgetActor.PersistenceIdOf(Site).ShouldBe("site-power-budget-slc-depot");

    [Fact]
    public async Task Given_a_busy_site_when_the_grid_curtails_then_versions_count_every_event()
    {
        var budget = Start();
        await AcceptAsync(budget, Configure(), 1);
        await AcceptAsync(budget, Request(Fleet1, BusSession), 2);
        await AcceptAsync(budget, Request(Public2, CarSession), 3);

        // Curtailment applied, and the allocations rebalanced: two events in one decision.
        await AcceptAsync(budget, new ApplyCurtailment(Command, Site, new(120m)), 5);
    }

    [Fact]
    public async Task Given_an_open_request_when_another_session_asks_then_it_is_rejected()
    {
        var budget = Start();
        await AcceptAsync(budget, Configure(), 1);
        await AcceptAsync(budget, Request(Fleet1, BusSession), 2);

        budget.Tell(Request(Fleet1, CarSession), TestActor);

        (await ExpectMsgAsync<BudgetCommandRejected>(cancellationToken: Ct))
            .Code.ShouldBe("connector-has-open-request");
    }

    [Fact]
    public async Task Given_an_unconfigured_site_when_power_is_requested_then_it_is_rejected()
    {
        var budget = Start();

        budget.Tell(Request(Fleet1, BusSession), TestActor);

        (await ExpectMsgAsync<BudgetCommandRejected>(cancellationToken: Ct))
            .Code.ShouldBe("not-configured");
    }

    [Fact]
    public async Task Given_a_configured_site_when_a_new_incarnation_starts_then_it_remembers()
    {
        var first = Start();
        await AcceptAsync(first, Configure(), 1);
        await WatchAsync(first);
        Sys.Stop(first);
        await ExpectTerminatedAsync(first, cancellationToken: Ct);

        var second = Start();
        second.Tell(Configure(), TestActor);

        (await ExpectMsgAsync<BudgetCommandRejected>(cancellationToken: Ct))
            .Code.ShouldBe("already-configured");
    }

    private IActorRef Start() => Sys.ActorOf(SitePowerBudgetActor.Props(Site, _clock));

    private async Task AcceptAsync(
        IActorRef budget, SitePowerBudgetCommand command, long version)
    {
        budget.Tell(command, TestActor);
        await ExpectMsgAsync(new BudgetCommandAccepted(version), cancellationToken: Ct);
    }
}
