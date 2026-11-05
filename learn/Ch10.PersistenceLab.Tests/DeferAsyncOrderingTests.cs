using Akka.Actor;
using Akka.Persistence.TestKit;
using Ch10.PersistenceLab.Tests.Counters;

namespace Ch10.PersistenceLab.Tests;

public sealed class DeferAsyncOrderingTests(ITestOutputHelper output)
    : PersistenceTestKit(nameof(DeferAsyncOrderingTests), output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_readings_in_flight_when_a_checkpoint_follows_then_it_runs_after_them()
    {
        var log = Sys.ActorOf(MeterLog.Props("meter-1", TestActor));

        var slowly = TimeSpan.FromMilliseconds(100);
        await WithJournalWrite(write => write.PassWithDelay(slowly), async () =>
        {
            log.Tell(new RecordReading(18_204_000));
            log.Tell(new RecordReading(18_204_350));
            log.Tell(new Checkpoint("07:45"));
            log.Tell(new RecordReading(18_204_700));

            await ExpectMsgAsync("stored 18204000 Wh", cancellationToken: Ct);
            await ExpectMsgAsync("stored 18204350 Wh", cancellationToken: Ct);
            await ExpectMsgAsync("checkpoint 07:45", cancellationToken: Ct);
            await ExpectMsgAsync("stored 18204700 Wh", cancellationToken: Ct);
        });
    }
}
