using Akka.Actor;
using Akka.Pattern;
using Akka.TestKit.Xunit;

namespace Ch02.AkkaBasics.Tests.Firmware;

public sealed class FirmwareSupervisionTests(ITestOutputHelper output) : TestKit(output: output)
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    private readonly FirmwareCatalogClient _catalog = new();

    public static TheoryData<Exception, Directive> Failures => new()
    {
        { new ArgumentException("A blank model."), Directive.Resume },
        {
            new FirmwareCatalogUnavailableException("VX-22", new HttpRequestException()),
            Directive.Restart
        },
        { new InvalidOperationException("A bug."), Directive.Escalate },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Given_a_failed_child_when_the_decider_sees_the_exception_then_it_chooses(
        Exception exception, Directive expected) =>
        FirmwareSupervision.Decide(exception).ShouldBe(expected);

    [Fact]
    public async Task Given_an_unreachable_catalog_when_the_lookup_crashes_then_it_restarts()
    {
        var ct = TestContext.Current.CancellationToken;
        var supervisor = Sys.ActorOf(SupervisedLookup());
        supervisor.Tell(new CheckFirmware("VX-22"), TestActor);
        await supervisor.Ask<Pong>(new Ping(), AskTimeout, ct); // The lookup is now in flight.

        _catalog.Fail("VX-22", new HttpRequestException("The catalog is down."));

        await ExpectMsgAsync(new FirmwareUnavailable("VX-22"), cancellationToken: ct);
        await AwaitAssertAsync(
            async () => (await RestartCount(supervisor)).ShouldBe(1), cancellationToken: ct);
        _catalog.Publish("VX-22", "4.3.0");
        await AwaitAssertAsync(
            async () => (await supervisor.Ask<FirmwareRelease>(
                    new CheckFirmware("VX-22"), TimeSpan.FromMilliseconds(200), ct))
                .Version.ShouldBe("4.3.0"),
            cancellationToken: ct);
    }

    [Fact]
    public async Task Given_a_restarted_lookup_when_it_runs_long_enough_then_the_backoff_resets()
    {
        var ct = TestContext.Current.CancellationToken;
        var supervisor = Sys.ActorOf(SupervisedLookup());
        supervisor.Tell(new CheckFirmware("VX-22"), TestActor);
        await supervisor.Ask<Pong>(new Ping(), AskTimeout, ct);
        _catalog.Fail("VX-22", new HttpRequestException("The catalog is down."));
        await AwaitAssertAsync(
            async () => (await RestartCount(supervisor)).ShouldBe(1), cancellationToken: ct);

        await AwaitAssertAsync(
            async () => (await RestartCount(supervisor)).ShouldBe(0),
            duration: TimeSpan.FromSeconds(3),
            cancellationToken: ct);
    }

    [Fact]
    public async Task Given_a_cached_release_when_a_bad_request_crashes_the_lookup_then_it_resumes()
    {
        var ct = TestContext.Current.CancellationToken;
        var supervisor = Sys.ActorOf(SupervisedLookup());
        _catalog.Publish("VX-22", "4.3.0");
        await supervisor.Ask<FirmwareRelease>(new CheckFirmware("VX-22"), AskTimeout, ct);

        supervisor.Tell(new CheckFirmware(""), TestActor); // ArgumentException: Resume.

        await supervisor.Ask<FirmwareRelease>(new CheckFirmware("VX-22"), AskTimeout, ct);
        _catalog.LookupCount.ShouldBe(1); // The cache survived: Resume keeps the actor's state.
        (await RestartCount(supervisor)).ShouldBe(0);
    }

    private Props SupervisedLookup() =>
        FirmwareSupervision.WithBackoff(
            FirmwareLookupActor.Props(_catalog),
            minBackoff: TimeSpan.FromMilliseconds(100),
            maxBackoff: TimeSpan.FromSeconds(1),
            resetAfter: TimeSpan.FromMilliseconds(500));

    private static async Task<int> RestartCount(IActorRef supervisor)
    {
        var reply = await supervisor.Ask<BackoffSupervisor.RestartCount>(
            BackoffSupervisor.GetRestartCount.Instance,
            AskTimeout,
            TestContext.Current.CancellationToken);
        return reply.Count;
    }
}
