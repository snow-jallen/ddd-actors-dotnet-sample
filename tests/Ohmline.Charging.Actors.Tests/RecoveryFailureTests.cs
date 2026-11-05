using Akka.Actor;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// When recovery fails, the actor stops: it cannot serve commands from a state it could not
/// rebuild. The test journal fails the replay on cue, as a database outage would.
/// </summary>
public sealed class RecoveryFailureTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(RecoveryFailureTests), output)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);

    private readonly FakeTimeProvider _clock = new(Morning);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_journal_that_fails_the_replay_when_recovering_then_the_actor_stops()
    {
        await RegisterAndStopAsync();

        await WithJournalRecovery(recovery => recovery.Fail(), async () =>
        {
            var recovering = Start();
            await WatchAsync(recovering);
            recovering.Tell(Status(), TestActor);

            await ExpectTerminatedAsync(recovering, cancellationToken: Ct);
        });
        await ExpectNoMsgAsync(Quiet, Ct); // The question that waited for the replay is lost.
    }

    [Fact]
    public async Task Given_a_failed_recovery_then_one_error_names_the_persistence_id()
    {
        await RegisterAndStopAsync();

        await WithJournalRecovery(recovery => recovery.Fail(), async () =>
            await EventFilter
                .Error(contains:
                    "Recovery of charge-point-OHM-SLC-0042 failed after sequence number 0.")
                .ExpectOneAsync(() =>
                {
                    Start();
                    return Task.CompletedTask;
                }, Ct));
    }

    private IActorRef Start() => Sys.ActorOf(ChargePointActor.Props(Charger, _clock));

    private async Task RegisterAndStopAsync()
    {
        var first = Start();
        first.Tell(Register(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await WatchAsync(first);
        Sys.Stop(first);
        await ExpectTerminatedAsync(first, cancellationToken: Ct);
    }
}
