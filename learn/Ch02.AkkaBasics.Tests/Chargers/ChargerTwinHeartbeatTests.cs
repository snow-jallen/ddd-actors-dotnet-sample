using Akka.Actor;
using Akka.TestKit.Xunit;

namespace Ch02.AkkaBasics.Tests.Chargers;

// These tests use a short real heartbeat interval. Every twin here is created with
// ChildActorOfAsync, which makes the TestActor its parent, so ExpectMsgAsync sees what the
// twin tells Context.Parent.
public sealed class ChargerTwinHeartbeatTests(ITestOutputHelper output) : TestKit(output: output)
{
    private const string ChargePointId = "OHM-SLC-0042";

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMilliseconds(200);

    private static readonly BootNotification Boot = new("Voltaro", "VX-22", "4.2.1");

    [Fact]
    public async Task Given_a_booted_twin_when_three_heartbeats_are_missed_then_its_parent_is_told()
    {
        var ct = TestContext.Current.CancellationToken;
        var twin = await ChildActorOfAsync(
            ChargerTwin.Props(ChargePointId, HeartbeatInterval), cancellationToken: ct);

        twin.Tell(Boot);

        await ExpectNoMsgAsync(HeartbeatInterval, ct); // One or two missed: not offline yet.
        await ExpectMsgAsync(new ChargerWentOffline(ChargePointId, 3), cancellationToken: ct);
    }

    [Fact]
    public async Task Given_a_booted_twin_when_heartbeats_keep_arriving_then_it_stays_online()
    {
        var ct = TestContext.Current.CancellationToken;
        var twin = await ChildActorOfAsync(
            ChargerTwin.Props(ChargePointId, HeartbeatInterval), cancellationToken: ct);
        twin.Tell(Boot);

        var sixIntervals = Task.Delay(HeartbeatInterval * 6, ct);
        while (!sixIntervals.IsCompleted)
        {
            twin.Tell(new Heartbeat());
            await Task.Delay(HeartbeatInterval / 4, ct);
        }

        await ExpectNoMsgAsync(TimeSpan.Zero, ct);
        (await StatusOf(twin)).ShouldBe(ChargerStatus.Available);
    }

    [Fact]
    public async Task Given_an_offline_twin_when_asked_for_status_then_it_reports_offline()
    {
        var twin = await OfflineTwin();

        (await StatusOf(twin)).ShouldBe(ChargerStatus.Offline);
    }

    [Fact]
    public async Task Given_an_offline_twin_when_the_charger_boots_again_then_it_is_watched_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var twin = await OfflineTwin();

        twin.Tell(Boot);

        (await StatusOf(twin)).ShouldBe(ChargerStatus.Available);
        await ExpectMsgAsync(new ChargerWentOffline(ChargePointId, 3), cancellationToken: ct);
    }

    private async Task<IActorRef> OfflineTwin()
    {
        var ct = TestContext.Current.CancellationToken;
        var twin = await ChildActorOfAsync(
            ChargerTwin.Props(ChargePointId, HeartbeatInterval), cancellationToken: ct);
        twin.Tell(Boot);
        await ExpectMsgAsync<ChargerWentOffline>(cancellationToken: ct);
        return twin;
    }

    private static async Task<ChargerStatus> StatusOf(IActorRef twin)
    {
        var report = await twin.Ask<ChargerStatusReport>(
            new GetStatus(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        return report.Status;
    }
}
