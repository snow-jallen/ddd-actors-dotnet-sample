using Microsoft.Extensions.Logging.Abstractions;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Monetary;
using static Ohmline.Charging.Actors.Tests.Journeys.TheJourney;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// The Development payment port: holds in memory, captures within them, and every call
/// idempotent by its key (section 15.6).
/// </summary>
public sealed class InMemoryPaymentPortTests
{
    private static readonly Money Twelve = new(12m, Currency.Usd);

    private static readonly IdempotencyKey HoldKey = IdempotencyKey.Of(Journey, "hold");

    private static readonly IdempotencyKey CaptureKey = IdempotencyKey.Of(Journey, "capture");

    private readonly InMemoryPaymentPort _port = new(NullLogger<InMemoryPaymentPort>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_hold_when_less_than_it_is_captured_then_that_much_is_taken()
    {
        var placed = await HoldAsync();

        (await _port.CaptureAsync(CaptureKey, placed.HoldId, Twelve, Ct))
            .ShouldBe(new PaymentCaptured(placed.HoldId, Twelve));
    }

    [Fact]
    public async Task Given_a_hold_when_more_than_it_is_captured_then_the_capture_fails()
    {
        var placed = await HoldAsync();

        (await _port.CaptureAsync(CaptureKey, placed.HoldId, Hold + Twelve, Ct))
            .ShouldBeOfType<CaptureFailed>().Code.ShouldBe("exceeds-hold");
    }

    [Fact]
    public async Task Given_a_voided_hold_when_it_is_captured_then_the_capture_fails()
    {
        var placed = await HoldAsync();
        await _port.VoidAsync(HoldKey, Ct);

        (await _port.CaptureAsync(CaptureKey, placed.HoldId, Twelve, Ct))
            .ShouldBeOfType<CaptureFailed>().Code.ShouldBe("unknown-hold");
    }

    [Fact]
    public async Task Given_a_hold_when_it_is_asked_for_again_under_its_key_then_it_is_the_same()
    {
        var first = await HoldAsync();

        (await HoldAsync()).ShouldBe(first);
    }

    [Fact]
    public async Task Given_a_capture_when_it_is_asked_for_again_under_its_key_then_it_is_the_same()
    {
        var placed = await HoldAsync();
        var first = await _port.CaptureAsync(CaptureKey, placed.HoldId, Twelve, Ct);

        // Taken once: a second capture under another key finds nothing left to take.
        (await _port.CaptureAsync(CaptureKey, placed.HoldId, Twelve, Ct)).ShouldBe(first);
        var another = IdempotencyKey.Of(Journey, "capture-2");
        (await _port.CaptureAsync(another, placed.HoldId, Twelve, Ct))
            .ShouldBeOfType<CaptureFailed>();
    }

    [Fact]
    public async Task Given_no_hold_under_a_key_when_it_is_voided_then_nothing_happens() =>
        (await _port.VoidAsync(HoldKey, Ct)).ShouldBe(new HoldVoided(HoldKey.Value));

    private async Task<HoldPlaced> HoldAsync() =>
        (await _port.PlaceHoldAsync(HoldKey, Driver, Hold, Ct)).ShouldBeOfType<HoldPlaced>();
}
