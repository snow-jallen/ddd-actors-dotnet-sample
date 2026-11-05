using Akka.Actor;
using Akka.TestKit.Xunit;

namespace Ch02.AkkaBasics.Tests.Chargers;

public sealed class ChargerTwinTests(ITestOutputHelper output) : TestKit(output: output)
{
    private const string ChargePointId = "OHM-SLC-0042";

    private static readonly BootNotification Boot = new("Voltaro", "VX-22", "4.2.1");

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task Given_a_booting_twin_when_asked_for_status_then_the_answer_waits_for_boot()
    {
        var ct = TestContext.Current.CancellationToken;
        var twin = Sys.ActorOf(ChargerTwin.Props(ChargePointId));

        twin.Tell(new GetStatus(), TestActor);
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), ct); // Stashed, not answered.
        twin.Tell(Boot, TestActor);

        await ExpectMsgAsync(
            new ChargerStatusReport(ChargePointId, ChargerStatus.Available, "4.2.1"),
            cancellationToken: ct);
    }

    [Fact]
    public async Task Given_a_booting_twin_when_a_status_arrives_first_then_it_applies_after_boot()
    {
        var twin = Sys.ActorOf(ChargerTwin.Props(ChargePointId));

        twin.Tell(new StatusChanged(ChargerStatus.Charging));
        twin.Tell(Boot);

        (await StatusOf(twin)).ShouldBe(ChargerStatus.Charging);
    }

    [Theory]
    [InlineData(ChargerStatus.Charging)]
    [InlineData(ChargerStatus.Faulted)]
    public async Task Given_an_available_twin_when_the_charger_reports_a_status_then_it_switches(
        ChargerStatus reported)
    {
        var twin = await BootedTwin();

        twin.Tell(new StatusChanged(reported));

        (await StatusOf(twin)).ShouldBe(reported);
    }

    [Fact]
    public async Task Given_a_charging_twin_when_the_charger_reports_available_then_it_switches()
    {
        var twin = await BootedTwin();
        twin.Tell(new StatusChanged(ChargerStatus.Charging));

        twin.Tell(new StatusChanged(ChargerStatus.Available));

        (await StatusOf(twin)).ShouldBe(ChargerStatus.Available);
    }

    [Fact]
    public async Task Given_a_faulted_twin_when_the_charger_reports_charging_then_it_stays_faulted()
    {
        var twin = await BootedTwin();
        twin.Tell(new StatusChanged(ChargerStatus.Faulted));

        twin.Tell(new StatusChanged(ChargerStatus.Charging));

        (await StatusOf(twin)).ShouldBe(ChargerStatus.Faulted);
    }

    [Fact]
    public async Task Given_a_faulted_twin_when_the_charger_restarts_then_it_is_available()
    {
        var twin = await BootedTwin();
        twin.Tell(new StatusChanged(ChargerStatus.Faulted));

        twin.Tell(Boot with { FirmwareVersion = "4.3.0" });

        var report = await twin.Ask<ChargerStatusReport>(
            new GetStatus(), AskTimeout, TestContext.Current.CancellationToken);
        report.ShouldBe(new ChargerStatusReport(ChargePointId, ChargerStatus.Available, "4.3.0"));
    }

    private async Task<IActorRef> BootedTwin()
    {
        var twin = Sys.ActorOf(ChargerTwin.Props(ChargePointId));
        twin.Tell(Boot);
        (await StatusOf(twin)).ShouldBe(ChargerStatus.Available);
        return twin;
    }

    private static async Task<ChargerStatus> StatusOf(IActorRef twin)
    {
        var report = await twin.Ask<ChargerStatusReport>(
            new GetStatus(), AskTimeout, TestContext.Current.CancellationToken);
        return report.Status;
    }
}
