using Akka.Actor;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Tests.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// What happens between an actor's construction and its first command. A new incarnation
/// replays the journal through Evolve and nothing else, so its state is the fold of the events,
/// and a command that arrives meanwhile waits for the replay to finish.
/// </summary>
public sealed class RecoveryTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(RecoveryTests), output)
{
    /// <summary>A registration, then ten reservations, each cancelled: 21 events.</summary>
    private const int Recorded = 21;

    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);

    private readonly FakeTimeProvider _clock = new(Morning);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string PersistenceId => ChargePointActor.PersistenceIdOf(Charger);

    [Fact]
    public async Task Given_21_events_when_a_new_incarnation_starts_then_its_state_is_their_fold()
    {
        await RecordHistoryAsync();
        var events = await JournalProbe.EventsAsync<ChargePointEvent>(Sys, PersistenceId, Ct);
        events.Length.ShouldBe(Recorded);

        var recovered = Start();
        recovered.Tell(Status(), TestActor);

        await ExpectMsgAsync(
            ChargePoint.Fold(events).ToStatus(Charger, Recorded), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_command_sent_during_recovery_then_it_is_answered_after_it()
    {
        await RecordHistoryAsync();
        var slowReplay = HeldWrite<ChargePointRegistered>.Create();
        await Journal.OnRecovery.SetInterceptorAsync(slowReplay);

        var recovering = Start();
        await slowReplay.Reached.WaitAsync(Dilated(TimeSpan.FromSeconds(3)), Ct);
        recovering.Tell(Reserve(2, DriversCard), TestActor);

        await ExpectNoMsgAsync(Quiet, Ct);
        slowReplay.Release();

        await ExpectMsgAsync(new CommandAccepted(Recorded + 1), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_recovery_then_one_info_line_says_what_it_replayed()
    {
        await RecordHistoryAsync();

        await EventFilter
            .Info(contains: $"{PersistenceId} recovered to sequence number {Recorded} in ")
            .ExpectOneAsync(() =>
            {
                Start();
                return Task.CompletedTask;
            }, Ct);
    }

    private IActorRef Start() => Sys.ActorOf(ChargePointActor.Props(Charger, _clock));

    /// <summary>Records the history with one incarnation, then stops it.</summary>
    private async Task RecordHistoryAsync()
    {
        var first = Start();
        await AcceptAsync(first, Register(), version: 1);
        for (var version = 2; version <= Recorded; version += 2)
        {
            await AcceptAsync(first, Reserve(1, DriversCard), version);
            await AcceptAsync(first, Cancel(1, DriversCard), version + 1);
        }

        await WatchAsync(first);
        Sys.Stop(first);
        await ExpectTerminatedAsync(first, cancellationToken: Ct);
    }

    private async Task AcceptAsync(IActorRef chargePoint, ChargePointCommand command, long version)
    {
        chargePoint.Tell(command, TestActor);
        await ExpectMsgAsync(new CommandAccepted(version), cancellationToken: Ct);
    }
}
