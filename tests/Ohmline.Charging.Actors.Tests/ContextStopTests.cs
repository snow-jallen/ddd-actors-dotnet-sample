using Akka.Actor;
using Akka.Event;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Actors.Tests.ChargePoints;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// The mistake on this branch: an ended session stops itself with <c>Context.Stop(Self)</c>
/// instead of asking its shard to passivate it. A probe plays the shard.
/// </summary>
public sealed class ContextStopTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(ContextStopTests), output)
{
    private static readonly DateTimeOffset Later = Morning.AddMinutes(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Demonstrates_the_session_stops_before_it_answers_and_loses_the_next_command()
    {
        var shard = CreateTestProbe();
        var session = shard.ChildActorOf(
            ChargingSessionActor.Props(Session, new FakeTimeProvider(Later)),
            cancellationToken: Ct);
        session.Tell(Begin(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        var deadLetters = CreateTestProbe();
        Sys.EventStream.Subscribe(deadLetters.Ref, typeof(DeadLetter));
        var slowJournal = HeldWrite<SessionEnded>.Create();
        await Journal.OnWrite.SetInterceptorAsync(slowJournal);

        session.Tell(new StopSession(Command, Session), TestActor);
        await slowJournal.Reached.WaitAsync(Dilated(TimeSpan.FromSeconds(3)), Ct);
        session.Tell(new SuspendCharging(Command, Session, new ByEv()), TestActor);
        slowJournal.Release();

        // It stopped while applying SessionEnded, before PersistAll's completion could reply:
        // the stop was stored, but its sender is never told. The command waiting behind it in
        // the stash becomes a dead letter, and the shard was never asked.
        var lost = await deadLetters.FishForMessageAsync<DeadLetter>(
            dead => dead.Message is SuspendCharging, cancellationToken: Ct);
        lost.Sender.ShouldBe(TestActor);
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), Ct);
        await shard.ExpectNoMsgAsync(TimeSpan.Zero, Ct);
    }

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
