using Akka.Persistence.TestKit;
using Ch10.PersistenceLab.Tests.Counters;

namespace Ch10.PersistenceLab.Tests;

public sealed class PersistStashesTests(ITestOutputHelper output)
    : PersistenceTestKit(nameof(PersistStashesTests), output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_write_in_flight_when_a_question_arrives_then_it_waits_for_it()
    {
        var counter = Sys.ActorOf(StashingCounter.Props("counter-1"));
        var journal = new HeldJournal();
        await Journal.OnWrite.SetInterceptorAsync(journal);

        counter.Tell(new Increment(), TestActor);
        await journal.Reached.WaitAsync(Dilated(TimeSpan.FromSeconds(3)), Ct);
        counter.Tell(new GetCount(), TestActor);

        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), Ct);
        journal.Release();
        await ExpectMsgAsync(new Counted(1), cancellationToken: Ct);
        await ExpectMsgAsync(new Count(1), cancellationToken: Ct);
    }
}
