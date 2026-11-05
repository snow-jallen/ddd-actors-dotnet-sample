using Microsoft.Extensions.Logging.Abstractions;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Monetary;
using static Ohmline.Charging.Actors.Tests.Journeys.TheJourney;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>The Development payment port: holds in memory, captures within them.</summary>
public sealed class InMemoryPaymentPortTests
{
    private static readonly Money Twelve = new(12m, Currency.Usd);

    private readonly InMemoryPaymentPort _port = new(NullLogger<InMemoryPaymentPort>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_hold_when_less_than_it_is_captured_then_that_much_is_taken()
    {
        var placed = (await _port.PlaceHoldAsync(Driver, Hold, Ct)).ShouldBeOfType<HoldPlaced>();

        (await _port.CaptureAsync(placed.HoldId, Twelve, Ct))
            .ShouldBe(new PaymentCaptured(placed.HoldId, Twelve));
    }

    [Fact]
    public async Task Given_a_hold_when_more_than_it_is_captured_then_the_capture_fails()
    {
        var placed = (await _port.PlaceHoldAsync(Driver, Hold, Ct)).ShouldBeOfType<HoldPlaced>();

        (await _port.CaptureAsync(placed.HoldId, Hold + Twelve, Ct))
            .ShouldBeOfType<CaptureFailed>().Code.ShouldBe("exceeds-hold");
    }

    [Fact]
    public async Task Given_a_voided_hold_when_it_is_captured_then_the_capture_fails()
    {
        var placed = (await _port.PlaceHoldAsync(Driver, Hold, Ct)).ShouldBeOfType<HoldPlaced>();
        await _port.VoidAsync(placed.HoldId, Ct);

        (await _port.CaptureAsync(placed.HoldId, Twelve, Ct))
            .ShouldBeOfType<CaptureFailed>().Code.ShouldBe("unknown-hold");
    }
}
