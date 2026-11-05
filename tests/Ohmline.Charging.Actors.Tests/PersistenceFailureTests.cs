using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// Errors are not failures. A rejected command is a reply; a journal that fails a write is a
/// failure, and the actor stops; a journal that rejects a write is answered as a rejection, and
/// the actor lives on. The test journal fails and rejects on cue.
/// </summary>
public sealed class PersistenceFailureTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(Serializers.Charging), nameof(PersistenceFailureTests), output)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);

    private readonly FakeTimeProvider _clock = new(Morning);

    private PersistenceShellSpec.Scenario ChargePoint =>
        Shell(ChargePointActor.Props(Charger, _clock), ChargePointActor.PersistenceIdOf(Charger));

    [Fact]
    public async Task Given_a_failed_write_then_the_actor_stops_and_its_sender_hears_nothing() =>
        await ChargePoint.Given(Registered())
            .When(Reserve(1, DriversCard))
            .WithJournalWrite(write => write.Fail())
            .ThenStopsAsync();

    [Fact]
    public async Task Given_a_failed_write_then_the_commands_stashed_behind_it_are_dead_letters()
    {
        var chargePoint = await ChargePoint.Given(Registered()).StartAsync();
        var deadLetters = CreateTestProbe();
        Sys.EventStream.Subscribe(deadLetters.Ref, typeof(DeadLetter));

        var waiting = Reserve(2, SomeoneElsesCard);

        await WithJournalWrite(write => write.FailWithDelay(Quiet), async () =>
        {
            chargePoint.Tell(Reserve(1, DriversCard), TestActor);
            chargePoint.Tell(waiting, TestActor); // Waits for the write.

            var lost = await deadLetters.FishForMessageAsync<DeadLetter>(
                letter => letter.Message is ReserveConnector, cancellationToken: Ct);
            lost.Message.ShouldBe(waiting);
        });
    }

    [Fact]
    public async Task Given_a_rejected_write_then_the_sender_is_told_and_the_actor_lives_on()
    {
        IActorRef chargePoint = ActorRefs.Nobody;
        await EventFilter
            .Error(start: "charge-point-OHM-SLC-0042 could not persist ConnectorReserved")
            .ExpectOneAsync(
                async () => chargePoint = await ChargePoint.Given(Registered())
                    .When(Reserve(1, DriversCard))
                    .WithJournalWrite(write => write.Reject())
                    .ThenAsync<CommandRejected>(
                        rejected => rejected.Code.ShouldBe("persistence-rejected")),
                Ct);
        await ExpectNoMsgAsync(Quiet, Ct); // And no CommandAccepted after it.

        chargePoint.Tell(Status(), TestActor);
        var status = await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Available); // Nothing was applied.
        status.Version.ShouldBe(2); // But the rejected event used up sequence number 2.
        chargePoint.Tell(Reserve(1, DriversCard), TestActor);
        await ExpectMsgAsync(new CommandAccepted(3), cancellationToken: Ct);
    }
}
