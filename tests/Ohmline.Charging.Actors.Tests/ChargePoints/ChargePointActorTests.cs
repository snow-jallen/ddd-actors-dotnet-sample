using Akka.Actor;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

/// <summary>
/// The shell, not the rules: what the actor replies, what it persists and what it remembers. The
/// rules themselves are the decider's tests. Every message, and every event on its way to the
/// test journal, goes through Charging's serializer.
/// </summary>
public sealed class ChargePointActorTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(ChargePointActorTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_message_is_serialized_on_its_way_and_props_are_not()
    {
        Sys.Settings.SerializeAllMessages.ShouldBeTrue();
        Sys.Settings.SerializeAllCreators.ShouldBeFalse();
    }

    [Fact]
    public async Task Given_a_new_charge_point_when_registered_then_it_is_accepted_at_version_1()
    {
        var chargePoint = Start();

        chargePoint.Tell(Register(), TestActor);

        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_accepted_commands_then_each_version_counts_the_events_persisted()
    {
        var chargePoint = Start();
        await Accept(chargePoint, Register(), version: 1);
        await Accept(chargePoint, Reserve(1, DriversCard), version: 2);
        _clock.Advance(TimeSpan.FromMinutes(16));

        // The lapsed reservation expires and the new one is made: two events in one decision.
        chargePoint.Tell(Reserve(1, SomeoneElsesCard), TestActor);

        await ExpectMsgAsync(new CommandAccepted(4), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_reservation_when_someone_else_plugs_in_then_nothing_is_persisted()
    {
        var chargePoint = Start();
        await Accept(chargePoint, Register(), version: 1);
        await Accept(chargePoint, Reserve(1, DriversCard), version: 2);

        chargePoint.Tell(Occupy(1, SomeoneElsesCard), TestActor);

        var rejected = await ExpectMsgAsync<CommandRejected>(cancellationToken: Ct);
        rejected.Code.ShouldBe("reserved-for-another-token");
        chargePoint.Tell(Status(), TestActor);
        (await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct)).Version.ShouldBe(2);
    }

    [Fact]
    public async Task Given_a_command_that_changes_nothing_then_it_is_accepted_at_the_same_version()
    {
        var chargePoint = Start();
        await Accept(chargePoint, Register(), version: 1);

        chargePoint.Tell(Release(2), TestActor);

        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_reservation_when_the_status_is_asked_then_it_shows_the_reservation()
    {
        var chargePoint = Start();
        await Accept(chargePoint, Register(), version: 1);
        await Accept(chargePoint, Reserve(1, DriversCard), version: 2);

        chargePoint.Tell(Status(), TestActor);

        var status = await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct);
        status.Version.ShouldBe(2);
        status.SiteId.ShouldBe(Depot);
        status.Connectors.ShouldBe(
        [
            new ConnectorStatus(
                1, new(22m), ConnectorStatus.Reserved, DriversCard, Morning.AddMinutes(15)),
            new ConnectorStatus(2, new(22m), ConnectorStatus.Available),
        ]);
    }

    [Fact]
    public async Task Given_no_registration_when_the_status_is_asked_then_there_are_no_connectors()
    {
        var chargePoint = Start();

        chargePoint.Tell(Status(), TestActor);

        await ExpectMsgAsync(
            new ChargePointStatus(Charger, 0, SiteId: null, Connectors: []), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_persisted_events_when_a_new_incarnation_starts_then_it_recovers_them()
    {
        var first = Start();
        await Accept(first, Register(), version: 1);
        await Accept(first, Reserve(1, DriversCard), version: 2);
        await StopAsync(first);

        var second = Start();
        second.Tell(Status(), TestActor);

        var status = await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct);
        status.Version.ShouldBe(2);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Reserved);
    }

    private IActorRef Start() => Sys.ActorOf(ChargePointActor.Props(Charger, _clock));

    private async Task Accept(IActorRef chargePoint, ChargePointCommand command, long version)
    {
        chargePoint.Tell(command, TestActor);
        await ExpectMsgAsync(new CommandAccepted(version), cancellationToken: Ct);
    }

    private async Task StopAsync(IActorRef actor)
    {
        await WatchAsync(actor);
        Sys.Stop(actor);
        await ExpectTerminatedAsync(actor, cancellationToken: Ct);
    }
}
