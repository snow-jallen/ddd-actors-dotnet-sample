namespace Ch02.ActorsByHand.Tests;

public sealed class DoubleBookingTests
{
    private const string ConnectorId = "OHM-SLC-0042/1";

    // How long a driver inside the token check waits for the other driver to get there too.
    private static readonly TimeSpan MeetingTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    public void Given_an_occupied_racy_connector_when_another_driver_taps_then_it_is_rejected()
    {
        var connector = new RacyConnector(ConnectorId, authorize: _ => true);
        connector.TryStartSession("session-a", "token-a").ShouldBeTrue();

        connector.TryStartSession("session-b", "token-b").ShouldBeFalse();

        connector.ActiveSession.ShouldBe("session-a");
    }

    [Fact]
    public async Task Given_a_free_racy_connector_when_two_drivers_tap_at_once_then_both_start()
    {
        using var bothChecked = new Barrier(participantCount: 2);
        var connector = new RacyConnector(ConnectorId, _ => WaitForOtherDriver(bothChecked));

        var started = await TwoDriversTapAtOnce(connector.TryStartSession);

        started.ShouldBe([true, true]);
    }

    [Fact]
    public async Task Given_a_free_locked_connector_when_two_drivers_tap_at_once_then_one_starts()
    {
        using var bothChecked = new Barrier(participantCount: 2);
        var connector = new LockedConnector(ConnectorId, _ => WaitForOtherDriver(bothChecked));

        var started = await TwoDriversTapAtOnce(connector.TryStartSession);

        started.Count(s => s).ShouldBe(1);
    }

    // Holds a driver inside the token check until the other driver is there too. The racy
    // connector lets both in; with the lock the other driver never arrives and the wait times out.
    private static bool WaitForOtherDriver(Barrier bothChecked)
    {
        bothChecked.SignalAndWait(MeetingTimeout);
        return true;
    }

    private static Task<bool[]> TwoDriversTapAtOnce(Func<string, string, bool> tryStartSession) =>
        Task.WhenAll(
            Task.Run(() => tryStartSession("session-a", "token-a")),
            Task.Run(() => tryStartSession("session-b", "token-b")));
}
