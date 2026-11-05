using Akka.Actor;
using Akka.Persistence.TestKit;
using Ch10.PersistenceLab.Tests.Counters;

namespace Ch10.PersistenceLab.Tests;

/// <summary>
/// PersistAll hands the journal one atomic write; Persist in a loop hands it one write per
/// event. What a failure leaves behind depends on the journal: these tests pin down the test
/// journal's behaviour, and section 10.9 shows a SQL journal refusing a whole batch.
/// </summary>
public sealed class PersistAllAtomicTests(ITestOutputHelper output)
    : PersistenceTestKit(nameof(PersistAllAtomicTests), output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_batch_when_the_journal_fails_its_write_then_none_of_it_is_stored()
    {
        var counter = await StartWithTwo(BatchCounter.Props, "batch-1");

        await WithJournalWrite(write => write.Fail(), async () =>
        {
            await WatchAsync(counter);
            counter.Tell(new Increment(Times: 3), TestActor);
            await ExpectTerminatedAsync(counter, cancellationToken: Ct);
        });

        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct); // No Counted: no callback ran.
        await ExpectRecoveredCount(BatchCounter.Props, "batch-1", 2);
    }

    [Fact]
    public async Task Given_one_persist_per_event_when_the_second_write_fails_then_the_first_stays()
    {
        var counter = await StartWithTwo(OneByOneCounter.Props, "one-by-one-1");

        await WithJournalWrite(write => write.FailIf(@event => @event.SequenceNr == 4), async () =>
        {
            await WatchAsync(counter);
            counter.Tell(new Increment(Times: 3), TestActor);
            await ExpectTerminatedAsync(counter, cancellationToken: Ct);
        });

        // Event 3 was a write of its own and is in the journal: the increment is half done.
        await ExpectRecoveredCount(OneByOneCounter.Props, "one-by-one-1", 3);
    }

    [Fact]
    public async Task Given_the_test_journal_when_part_of_a_batch_fails_then_earlier_events_stay()
    {
        var counter = await StartWithTwo(BatchCounter.Props, "batch-2");

        // The test journal checks each event of a write on its own and keeps the ones before a
        // failure. A database journal writes the batch in one transaction (section 10.9), so
        // test atomicity against the real journal, not this one.
        await WithJournalWrite(write => write.FailIf(@event => @event.SequenceNr == 4), async () =>
        {
            await WatchAsync(counter);
            counter.Tell(new Increment(Times: 3), TestActor);
            await ExpectTerminatedAsync(counter, cancellationToken: Ct);
        });

        await ExpectRecoveredCount(BatchCounter.Props, "batch-2", 3);
    }

    private async Task<IActorRef> StartWithTwo(Func<string, Props> props, string id)
    {
        var counter = Sys.ActorOf(props(id));
        counter.Tell(new Increment(Times: 2), TestActor);
        counter.Tell(new GetCount(), TestActor);
        await FishForMessageAsync<Count>(count => count.Value == 2, cancellationToken: Ct);
        return counter;
    }

    private async Task ExpectRecoveredCount(Func<string, Props> props, string id, int count)
    {
        var revived = Sys.ActorOf(props(id));
        revived.Tell(new GetCount(), TestActor);
        await ExpectMsgAsync(new Count(count), cancellationToken: Ct);
    }
}
