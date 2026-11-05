namespace Ch02.ActorsByHand.Tests;

public sealed class ReplyAndAskTests
{
    private const string ConnectorId = "OHM-SLC-0042/1";

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Given_a_reply_address_in_a_message_when_told_then_the_reply_goes_there()
    {
        var statuses = new List<ConnectorStatus>();
        await using (var statusBoard = new Mailbox<ConnectorStatus>(statuses.Add))
        await using (var connector = new ConnectorActor(ConnectorId, _ => true, _ => { }))
        {
            connector.Tell(new StartSession("session-a", "token-a"));
            connector.Tell(new GetStatus(ReplyTo: statusBoard.Tell));
        }

        statuses.ShouldBe([new ConnectorStatus(ConnectorId, "session-a")]);
    }

    [Fact]
    public async Task Given_an_occupied_connector_when_asked_for_its_status_then_it_answers()
    {
        await using var connector = new ConnectorActor(ConnectorId, _ => true, _ => { });
        connector.Tell(new StartSession("session-a", "token-a"));

        var status = await connector.AskAsync(
            (ReplyTo<ConnectorStatus> replyTo) => new GetStatus(replyTo),
            AskTimeout,
            TestContext.Current.CancellationToken);

        status.ShouldBe(new ConnectorStatus(ConnectorId, "session-a"));
    }

    [Fact]
    public async Task Given_a_busy_connector_when_asked_with_a_short_timeout_then_it_times_out()
    {
        using var tokenServiceAnswers = new ManualResetEventSlim();
        await using var connector = new ConnectorActor(
            ConnectorId, _ => { tokenServiceAnswers.Wait(); return true; }, _ => { });
        try
        {
            // The actor sits in a slow token check; the question waits behind it in the mailbox.
            connector.Tell(new StartSession("session-a", "token-a"));

            await Should.ThrowAsync<TimeoutException>(() =>
                connector.AskAsync(
                    (ReplyTo<ConnectorStatus> replyTo) => new GetStatus(replyTo),
                    TimeSpan.FromMilliseconds(100),
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            tokenServiceAnswers.Set();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)] // Timeout.InfiniteTimeSpan
    public async Task Given_any_connector_when_asked_without_a_timeout_then_the_ask_is_refused(
        int timeoutMilliseconds)
    {
        await using var connector = new ConnectorActor(ConnectorId, _ => true, _ => { });

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            connector.AskAsync(
                (ReplyTo<ConnectorStatus> replyTo) => new GetStatus(replyTo),
                TimeSpan.FromMilliseconds(timeoutMilliseconds),
                TestContext.Current.CancellationToken));
    }
}
