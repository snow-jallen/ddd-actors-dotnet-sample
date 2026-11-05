using Akka.Actor;
using Akka.TestKit.Xunit;

namespace Ch02.AkkaBasics.Tests.Connectors;

// The TestKit gives every test a fresh ActorSystem (Sys) and a TestActor that records the
// messages sent to it.
public sealed class AkkaConnectorActorTests(ITestOutputHelper output) : TestKit(output: output)
{
    private const string ConnectorId = "OHM-SLC-0042/1";

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task Given_a_free_connector_when_a_session_starts_then_the_sender_is_told()
    {
        var connector = Sys.ActorOf(AkkaConnectorActor.Props(ConnectorId));

        connector.Tell(new StartSession("session-a"), sender: TestActor);

        await ExpectMsgAsync(
            new SessionStarted(ConnectorId, "session-a"),
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_an_occupied_connector_when_another_session_starts_then_it_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var connector = Sys.ActorOf(AkkaConnectorActor.Props(ConnectorId));
        connector.Tell(new StartSession("session-a"), TestActor);
        await ExpectMsgAsync<SessionStarted>(cancellationToken: ct);

        connector.Tell(new StartSession("session-b"), TestActor);

        await ExpectMsgAsync(new SessionRejected(ConnectorId, "session-b"), cancellationToken: ct);
    }

    [Fact]
    public async Task Given_ten_drivers_when_they_all_tap_at_once_then_exactly_one_session_starts()
    {
        var ct = TestContext.Current.CancellationToken;
        var connector = Sys.ActorOf(AkkaConnectorActor.Props(ConnectorId));
        var drivers = Enumerable.Range(1, 10).Select(i => CreateTestProbe($"driver-{i}")).ToList();

        Parallel.ForEach(drivers, driver =>
            connector.Tell(new StartSession($"session-{driver.Ref.Path.Name}"), driver.Ref));

        var replies = new List<ConnectorEvent>();
        foreach (var driver in drivers)
            replies.Add(await driver.ExpectMsgAsync<ConnectorEvent>(cancellationToken: ct));
        replies.OfType<SessionStarted>().ShouldHaveSingleItem();
        replies.OfType<SessionRejected>().Count().ShouldBe(9);
    }

    [Fact]
    public async Task Given_an_occupied_connector_when_asked_for_its_status_then_it_answers()
    {
        var ct = TestContext.Current.CancellationToken;
        var connector = Sys.ActorOf(AkkaConnectorActor.Props(ConnectorId));
        connector.Tell(new StartSession("session-a"), TestActor);

        // Always pass a timeout: Akka.NET's default ask timeout is infinite.
        var status = await connector.Ask<ConnectorStatus>(new GetStatus(), AskTimeout, ct);

        status.ShouldBe(new ConnectorStatus(ConnectorId, "session-a"));
    }

    [Fact]
    public async Task Given_a_free_connector_when_asked_what_it_ignores_then_the_ask_times_out()
    {
        var connector = Sys.ActorOf(AkkaConnectorActor.Props(ConnectorId));

        await Should.ThrowAsync<AskTimeoutException>(() =>
            connector.Ask<SessionEnded>(
                new EndSession("session-never-started"),
                TimeSpan.FromMilliseconds(200),
                TestContext.Current.CancellationToken));
    }
}
