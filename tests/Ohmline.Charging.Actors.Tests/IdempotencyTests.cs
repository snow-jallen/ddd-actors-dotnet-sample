using Akka.Actor;
using Microsoft.Extensions.Time.Testing;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// A command sent again with the same id, by a client that timed out or by a delivery that
/// repeated itself, gets the answer it got the first time, and changes nothing: in the same
/// incarnation, in the next one, which rebuilds the window from its events, and after a
/// snapshot, which carries the window.
/// </summary>
public sealed class IdempotencyTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(Serializers.Charging), nameof(IdempotencyTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private PersistenceShellSpec.Scenario ChargePoint =>
        Shell(ChargePointActor.Props(Charger, _clock), ChargePointActor.PersistenceIdOf(Charger));

    [Fact]
    public async Task Given_an_accepted_command_when_it_arrives_again_then_it_changes_nothing()
    {
        var chargePoint = await ChargePoint.Given(Registered()).StartAsync();
        var reserve = Reserve(1, DriversCard);

        chargePoint.Tell(reserve, TestActor);
        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);
        chargePoint.Tell(Cancel(1, DriversCard), TestActor);
        await ExpectMsgAsync(new CommandAccepted(3), cancellationToken: Ct);
        chargePoint.Tell(reserve, TestActor); // The first reservation, sent again.

        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);
        chargePoint.Tell(Status(), TestActor);
        var status = await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct);
        status.Version.ShouldBe(3);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Available);
    }

    [Fact]
    public async Task Given_its_events_in_the_journal_when_sent_again_then_it_changes_nothing()
    {
        var reserve = Reserve(1, DriversCard);

        await ChargePoint.Given(
                Registered(),
                Reserved(1, DriversCard, by: reserve.CommandId),
                Stamped(new ReservationCancelled(Charger, 1, DriversCard)))
            .When(reserve)
            .ThenAsync(new CommandAccepted(2)); // And nothing persisted.
    }

    [Fact]
    public async Task Given_a_command_from_before_the_snapshot_when_it_arrives_again_then_too()
    {
        var first = Sys.ActorOf(ChargePointActor.Props(Charger, _clock));
        var commands = History(100).ToList();
        foreach (var (command, version) in commands.Select((command, index) => (command, index)))
        {
            first.Tell(command, TestActor);
            await ExpectMsgAsync(new CommandAccepted(version + 1), cancellationToken: Ct);
        }

        await WatchAsync(first);
        first.Tell(Stop.Instance);
        await ExpectTerminatedAsync(first, cancellationToken: Ct);

        IActorRef second = ActorRefs.Nobody;
        await EventFilter
            .Info(contains: ": snapshot at 100, 0 events replayed")
            .ExpectOneAsync(() =>
            {
                second = Sys.ActorOf(ChargePointActor.Props(Charger, _clock));
                return Task.CompletedTask;
            }, Ct);
        second.Tell(commands[49], TestActor);

        await ExpectMsgAsync(new CommandAccepted(50), cancellationToken: Ct);
    }

    /// <summary>A registration, then a reservation and its cancellation, over and over.</summary>
    private static IEnumerable<ChargePointCommand> History(int commands) =>
    [
        Register(),
        .. Enumerable.Range(2, commands - 1).Select(version => version % 2 == 0
            ? (ChargePointCommand)Reserve(1, DriversCard)
            : Cancel(1, DriversCard)),
    ];
}
