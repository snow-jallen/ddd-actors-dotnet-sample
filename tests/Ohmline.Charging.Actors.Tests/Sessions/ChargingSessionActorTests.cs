using Akka.Actor;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sessions;

/// <summary>
/// The session's shell: the same command path as the charge point's, through DeciderActor.
/// Only what the shell does is tested here; the session's rules are the decider's tests.
/// </summary>
public sealed class ChargingSessionActorTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(ChargingSessionActorTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_persistence_id_is_the_durable_type_name_and_the_session_id() =>
        ChargingSessionActor.PersistenceIdOf(Session).ShouldBe($"charging-session-{Session}");

    [Fact]
    public async Task Given_a_session_when_it_starts_meters_and_stops_then_versions_count_events()
    {
        var session = Start();

        await AcceptAsync(session, Begin(), 1);
        await AcceptAsync(session, new RecordMeterValue(Command, Session, 1_207_400, Later), 2);
        await AcceptAsync(session, new StopSession(Command, Session), 3);
    }

    [Fact]
    public async Task Given_a_started_session_when_it_is_started_again_then_it_is_rejected()
    {
        var session = Start();
        await AcceptAsync(session, Begin(), 1);

        session.Tell(Begin(), TestActor);

        (await ExpectMsgAsync<CommandRejected>(cancellationToken: Ct))
            .Code.ShouldBe("already-started");
    }

    [Fact]
    public async Task Given_no_session_when_a_meter_value_arrives_then_it_is_rejected()
    {
        var session = Start();

        session.Tell(new RecordMeterValue(Command, Session, 1_207_400, Later), TestActor);

        (await ExpectMsgAsync<CommandRejected>(cancellationToken: Ct)).Code.ShouldBe("not-started");
    }

    [Fact]
    public async Task Given_an_ended_session_when_a_new_incarnation_starts_then_it_remembers()
    {
        var first = Start();
        await AcceptAsync(first, Begin(), 1);
        await AcceptAsync(first, new StopSession(Command, Session), 2);
        await WatchAsync(first);
        Sys.Stop(first);
        await ExpectTerminatedAsync(first, cancellationToken: Ct);

        var second = Start();
        second.Tell(new SuspendCharging(Command, Session, new ByEv()), TestActor);

        (await ExpectMsgAsync<CommandRejected>(cancellationToken: Ct))
            .Code.ShouldBe("already-ended");
    }

    private static DateTimeOffset Later => Morning.AddMinutes(10);

    private static StartSession Begin() => new(
        Command,
        Session,
        Depot,
        ChargingSamples.Connector,
        DriversCard,
        ChargingSamples.Tariff,
        ChargingSamples.SpendCap,
        MeterStartWh: 1_200_000);

    private IActorRef Start() => Sys.ActorOf(ChargingSessionActor.Props(Session, _clock));

    private async Task AcceptAsync(IActorRef session, SessionCommand command, long version)
    {
        session.Tell(command, TestActor);
        await ExpectMsgAsync(new CommandAccepted(version), cancellationToken: Ct);
    }
}
