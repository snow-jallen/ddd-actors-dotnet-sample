namespace Ch02.ActorsByHand.Tests;

public sealed class SupervisionTests
{
    private const string ConnectorId = "OHM-SLC-0042/1";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private readonly TaskCompletionSource<Exception> _restarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Given_an_occupied_connector_when_it_crashes_then_the_restart_forgets_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connector = new Supervisor(
            () => new ConnectorActor(ConnectorId, AuthorizeUnlessBlocked, PublishToFailingBoard));
        connector.ChildRestarted += exception => _restarted.TrySetResult(exception);
        connector.Tell(new StartSession("session-a", "token-a"));
        (await GetStatusAsync(connector, ct)).ActiveSession.ShouldBe("session-a");

        connector.Tell(new StartSession("session-b", "token-b")); // Rejected, and publishing fails.

        (await _restarted.Task.WaitAsync(Timeout, ct)).ShouldBeOfType<InvalidOperationException>();
        // Session A is still charging, but the restarted connector no longer knows about it.
        (await GetStatusAsync(connector, ct)).ActiveSession.ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_crashed_connector_when_it_has_restarted_then_its_address_works()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connector = new Supervisor(
            () => new ConnectorActor(ConnectorId, AuthorizeUnlessBlocked, PublishToFailingBoard));
        connector.ChildRestarted += exception => _restarted.TrySetResult(exception);
        connector.Tell(new StartSession("session-a", "token-blocked"));
        await _restarted.Task.WaitAsync(Timeout, ct);

        connector.Tell(new StartSession("session-b", "token-b"));

        (await GetStatusAsync(connector, ct)).ActiveSession.ShouldBe("session-b");
    }

    [Fact]
    public async Task Given_a_directory_when_one_id_is_looked_up_twice_then_it_is_one_actor()
    {
        await using var directory = new ActorDirectory<ConnectorActor>(
            id => new ConnectorActor(id, _ => true, _ => { }));

        var first = directory.GetOrCreate("OHM-SLC-0042/1");
        var second = directory.GetOrCreate("OHM-SLC-0042/1");
        var other = directory.GetOrCreate("OHM-SLC-0042/2");

        second.ShouldBeSameAs(first);
        other.ShouldNotBeSameAs(first);
    }

    [Fact]
    public async Task Given_a_directory_when_many_threads_look_up_one_id_then_one_actor_is_created()
    {
        var created = 0;
        await using var directory = new ActorDirectory<ConnectorActor>(id =>
        {
            Interlocked.Increment(ref created);
            return new ConnectorActor(id, _ => true, _ => { });
        });

        var found = new ConnectorActor[100];
        Parallel.For(0, found.Length, i => found[i] = directory.GetOrCreate(ConnectorId));

        created.ShouldBe(1);
        found.ShouldAllBe(actor => ReferenceEquals(actor, found[0]));
    }

    [Fact]
    public async Task Given_supervised_connectors_in_a_directory_when_used_then_each_has_its_state()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connectors = new ActorDirectory<Supervisor>(
            id => new Supervisor(() => new ConnectorActor(id, _ => true, _ => { })));

        connectors.GetOrCreate("OHM-SLC-0042/1").Tell(new StartSession("session-a", "token-a"));
        connectors.GetOrCreate("OHM-SLC-0042/2").Tell(new StartSession("session-b", "token-b"));

        (await GetStatusAsync(connectors.GetOrCreate("OHM-SLC-0042/1"), ct))
            .ActiveSession.ShouldBe("session-a");
        (await GetStatusAsync(connectors.GetOrCreate("OHM-SLC-0042/2"), ct))
            .ActiveSession.ShouldBe("session-b");
    }

    private static bool AuthorizeUnlessBlocked(string tokenUid) => tokenUid != "token-blocked";

    // A subscriber that fails runs inside the connector's turn, so its exception crashes the
    // connector. Here the site's live board cannot show rejections.
    private static void PublishToFailingBoard(ConnectorEvent connectorEvent)
    {
        if (connectorEvent is SessionRejected)
            throw new InvalidOperationException("The live board is unavailable.");
    }

    private static Task<ConnectorStatus> GetStatusAsync(
        ICanTell<ConnectorMessage> connector, CancellationToken ct) =>
        connector.AskAsync(
            (ReplyTo<ConnectorStatus> replyTo) => new GetStatus(replyTo), Timeout, ct);
}
