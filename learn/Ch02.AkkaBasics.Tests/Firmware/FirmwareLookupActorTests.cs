using Akka.Actor;
using Akka.TestKit.Xunit;

namespace Ch02.AkkaBasics.Tests.Firmware;

public sealed class FirmwareLookupActorTests(ITestOutputHelper output) : TestKit(output: output)
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    private readonly FirmwareCatalogClient _catalog = new();

    [Fact]
    public async Task Given_a_lookup_in_flight_when_pinged_then_the_actor_answers_at_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var lookup = Sys.ActorOf(FirmwareLookupActor.Props(_catalog));
        lookup.Tell(new CheckFirmware("VX-22"), TestActor);

        await lookup.Ask<Pong>(new Ping(), AskTimeout, ct); // The mailbox is not suspended.

        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), ct);
        _catalog.Publish("VX-22", "4.3.0");
        await ExpectMsgAsync(new FirmwareRelease("VX-22", "4.3.0"), cancellationToken: ct);
    }

    [Fact]
    public async Task Given_a_lookup_in_flight_when_asked_again_then_the_request_waits_for_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var lookup = Sys.ActorOf(FirmwareLookupActor.Props(_catalog));
        var first = CreateTestProbe();
        var second = CreateTestProbe();
        lookup.Tell(new CheckFirmware("VX-22"), first.Ref);

        lookup.Tell(new CheckFirmware("VX-22"), second.Ref); // Stashed while Loading.
        await second.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), ct);
        _catalog.Publish("VX-22", "4.3.0");

        await first.ExpectMsgAsync(new FirmwareRelease("VX-22", "4.3.0"), cancellationToken: ct);
        await second.ExpectMsgAsync(new FirmwareRelease("VX-22", "4.3.0"), cancellationToken: ct);
        _catalog.LookupCount.ShouldBe(1); // The second answer came from the cache.
    }

    [Fact]
    public async Task Given_a_receive_async_handler_awaiting_io_when_pinged_then_the_ping_waits()
    {
        var ct = TestContext.Current.CancellationToken;
        var lookup = Sys.ActorOf(BlockingFirmwareLookupActor.Props(_catalog));
        var requester = CreateTestProbe();
        lookup.Tell(new CheckFirmware("VX-22"), requester.Ref);

        lookup.Tell(new Ping(), TestActor);

        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct); // The mailbox is suspended.
        _catalog.Publish("VX-22", "4.3.0");
        // The reply after the await still goes to the original sender, not to the Ping's.
        await requester.ExpectMsgAsync(
            new FirmwareRelease("VX-22", "4.3.0"), cancellationToken: ct);
        await ExpectMsgAsync<Pong>(cancellationToken: ct);
    }
}
