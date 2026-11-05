using Akka.Persistence.TestKit;
using Ch10.PersistenceLab.Tests.Counters;

namespace Ch10.PersistenceLab.Tests;

public sealed class PersistAsyncInterleavesTests(ITestOutputHelper output)
    : PersistenceTestKit(nameof(PersistAsyncInterleavesTests), output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_write_in_flight_when_a_question_arrives_then_it_sees_the_old_count()
    {
        var counter = Sys.ActorOf(InterleavingCounter.Props("counter-1"));
        var journal = new HeldJournal();
        await Journal.OnWrite.SetInterceptorAsync(journal);

        counter.Tell(new Increment(), TestActor);
        await journal.Reached.WaitAsync(Dilated(TimeSpan.FromSeconds(3)), Ct);
        counter.Tell(new GetCount(), TestActor);

        // Answered while the event is still being written: the count does not include it.
        await ExpectMsgAsync(new Count(0), cancellationToken: Ct);
        journal.Release();
        await ExpectMsgAsync(new Counted(1), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_two_increments_back_to_back_then_both_are_handled_before_any_is_stored()
    {
        var counter = Sys.ActorOf(InterleavingCounter.Props("counter-2"));
        var journal = new HeldJournal();
        await Journal.OnWrite.SetInterceptorAsync(journal);

        counter.Tell(new Increment(), TestActor);
        counter.Tell(new Increment(), TestActor);
        counter.Tell(new GetCount(), TestActor);

        await ExpectMsgAsync(new Count(0), cancellationToken: Ct);
        journal.Release();
        await ExpectMsgAsync(new Counted(1), cancellationToken: Ct);
        await ExpectMsgAsync(new Counted(2), cancellationToken: Ct);
    }
}
