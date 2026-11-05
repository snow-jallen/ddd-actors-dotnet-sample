namespace Ch02.ActorsByHand.Tests;

public sealed class ConnectorActorTests
{
    private const string ConnectorId = "OHM-SLC-0042/1";

    private static readonly TimeSpan MeetingTimeout = TimeSpan.FromSeconds(1);

    // Only the mailbox loop adds to _events; each test reads it after the loop has finished.
    private readonly List<ConnectorEvent> _events = [];

    [Fact]
    public async Task Given_a_free_connector_when_two_drivers_tap_at_once_then_one_session_starts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var bothChecked = new Barrier(participantCount: 2);
        await using (var connector = new ConnectorActor(
            ConnectorId, _ => WaitForOtherDriver(bothChecked), _events.Add))
        {
            await Task.WhenAll(
                Task.Run(() => connector.Tell(new StartSession("session-a", "token-a")), ct),
                Task.Run(() => connector.Tell(new StartSession("session-b", "token-b")), ct));
        } // Disposing the actor processes every queued message, then stops the loop.

        _events.OfType<SessionStarted>().ShouldHaveSingleItem();
        _events.OfType<SessionRejected>().ShouldHaveSingleItem()
            .Reason.ShouldBe(RejectionReason.ConnectorOccupied);
    }

    [Fact]
    public async Task Given_a_free_connector_when_fifty_drivers_tap_at_once_then_one_starts()
    {
        await using (var connector = new ConnectorActor(ConnectorId, _ => true, _events.Add))
        {
            Parallel.For(0, 50, i =>
                connector.Tell(new StartSession($"session-{i}", $"token-{i}")));
        }

        _events.OfType<SessionStarted>().ShouldHaveSingleItem();
        _events.OfType<SessionRejected>().Count().ShouldBe(49);
    }

    [Fact]
    public async Task Given_an_occupied_connector_when_the_session_ends_then_a_new_one_starts()
    {
        await using (var connector = new ConnectorActor(ConnectorId, _ => true, _events.Add))
        {
            connector.Tell(new StartSession("session-a", "token-a"));
            connector.Tell(new EndSession("session-a"));
            connector.Tell(new EndSession("session-a"));
            connector.Tell(new StartSession("session-b", "token-b"));
        }

        _events.ShouldBe(
        [
            new SessionStarted(ConnectorId, "session-a"),
            new SessionEnded(ConnectorId, "session-a"),
            new SessionStarted(ConnectorId, "session-b"),
        ]);
    }

    [Fact]
    public async Task Given_a_free_connector_when_the_token_is_not_authorized_then_it_is_rejected()
    {
        await using (var connector = new ConnectorActor(ConnectorId, _ => false, _events.Add))
        {
            connector.Tell(new StartSession("session-a", "token-blocked"));
        }

        _events.ShouldBe(
        [
            new SessionRejected(ConnectorId, "session-a", RejectionReason.TokenNotAuthorized),
        ]);
    }

    // The same wait as in DoubleBookingTests. The actor handles one message at a time, so the
    // second driver's message waits in the mailbox and never reaches the token check alongside.
    private static bool WaitForOtherDriver(Barrier bothChecked)
    {
        bothChecked.SignalAndWait(MeetingTimeout);
        return true;
    }
}
