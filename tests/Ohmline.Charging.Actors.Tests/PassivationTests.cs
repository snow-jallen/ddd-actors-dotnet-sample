using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Persistence.TestKit;
using Akka.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Actors.Tests.ChargePoints;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// How an entity stops: only when its shard tells it <see cref="Stop"/>, and never before it
/// has answered what arrived first. A test probe plays the shard, as the entity's parent.
/// </summary>
public sealed class PassivationTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(PassivationTests), output)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);

    private static readonly DateTimeOffset Later = Morning.AddMinutes(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_write_in_flight_when_told_to_stop_then_it_answers_first()
    {
        var shard = CreateTestProbe();
        var chargePoint = shard.ChildActorOf(
            ChargePointActor.Props(Charger, new FakeTimeProvider(Morning)),
            cancellationToken: Ct);
        chargePoint.Tell(Register(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await WatchAsync(chargePoint);
        var slowJournal = HeldWrite<ConnectorReserved>.Create();
        await Journal.OnWrite.SetInterceptorAsync(slowJournal);

        chargePoint.Tell(Reserve(1, DriversCard), TestActor);
        await slowJournal.Reached.WaitAsync(Dilated(TimeSpan.FromSeconds(3)), Ct);
        chargePoint.Tell(Stop.Instance, shard.Ref);
        await ExpectNoMsgAsync(Quiet, Ct);
        slowJournal.Release();

        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);
        await ExpectTerminatedAsync(chargePoint, cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_session_that_ends_then_it_asks_its_shard_to_passivate_it()
    {
        var shard = CreateTestProbe();
        var session = SessionUnder(shard);
        session.Tell(Begin(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await shard.ExpectNoMsgAsync(Quiet, Ct); // A running session stays.

        session.Tell(new StopSession(Command, Session), TestActor);

        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);
        var passivate = await shard.ExpectMsgAsync<Passivate>(cancellationToken: Ct);
        passivate.StopMessage.ShouldBe(Stop.Instance);
    }

    [Fact]
    public async Task Given_a_command_behind_the_last_write_then_it_is_answered_before_the_stop()
    {
        var shard = CreateTestProbe();
        var session = SessionUnder(shard);
        session.Tell(Begin(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await WatchAsync(session);
        var slowJournal = HeldWrite<SessionEnded>.Create();
        await Journal.OnWrite.SetInterceptorAsync(slowJournal);

        session.Tell(new StopSession(Command, Session), TestActor);
        await slowJournal.Reached.WaitAsync(Dilated(TimeSpan.FromSeconds(3)), Ct);
        session.Tell(new SuspendCharging(Command, Session, new ByEv()), TestActor);
        slowJournal.Release();

        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);
        (await ExpectMsgAsync<CommandRejected>(cancellationToken: Ct)).Code
            .ShouldBe("already-ended");
        var passivate = await shard.ExpectMsgAsync<Passivate>(cancellationToken: Ct);
        session.Tell(passivate.StopMessage, shard.Ref); // What the shard does next.
        await ExpectTerminatedAsync(session, cancellationToken: Ct);
    }

    private static IActorRef SessionUnder(TestProbe shard) => shard.ChildActorOf(
        ChargingSessionActor.Props(Session, new FakeTimeProvider(Later)),
        cancellationToken: Ct);

    private static StartSession Begin() => new(
        Command,
        Session,
        Depot,
        ChargingSamples.Connector,
        DriversCard,
        ChargingSamples.Tariff,
        ChargingSamples.SpendCap,
        MeterStartWh: 1_200_000);
}
