using System.Collections.Immutable;
using System.Text.Json;
using Ohmline.Billing.Contracts.Holds;
using Ohmline.Billing.Contracts.Receipts;
using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.ContractTests;

/// <summary>
/// One message of every published shape, with every field filled in: a 30 kWh session at the
/// depot on 2026-11-04, the hold the journey placed for it, and its receipt. The golden files
/// are these, as JSON.
/// </summary>
internal static class Samples
{
    public static readonly Guid Session = Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000a01");

    public static readonly DateTimeOffset Started = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset Ended = Started.AddMinutes(45);

    public static readonly string HoldId = $"{Session}-hold";

    public static SessionCompleted SessionCompleted { get; } = new(
        Guid.Parse("019a4f6e-1c40-7f3e-9c2a-5d1b00000b01"),
        Ended,
        Session,
        "04A2B3C4D5E6F7",
        "OHM-SLC-0042/1",
        30m,
        new PriceBreakdown(
            "USD",
            13.60m,
            [
                new PriceLine(PriceLine.Energy, 30m, "kWh", 0.42m, 12.60m),
                new PriceLine(PriceLine.SessionFee, 1m, "session", 1m, 1m),
            ]),
        Started,
        Ended);

    /// <summary>The same session in the second version (section 20.9), under its own id.</summary>
    public static SessionCompletedV2 SessionCompletedV2 { get; } = new(
        Guid.Parse("019a4f6e-1c40-7f3e-9c2a-5d1b00000b02"),
        Ended,
        Session,
        "slc-depot",
        "04A2B3C4D5E6F7",
        "OHM-SLC-0042",
        1,
        30m,
        SessionCompleted.Price,
        Started,
        Ended);

    /// <summary>Every integration event, by its wire name.</summary>
    public static ImmutableDictionary<string, ChargingIntegrationEvent> IntegrationEvents
    {
        get;
    } = new Dictionary<string, ChargingIntegrationEvent>
    {
        [SessionCompleted.WireName] = SessionCompleted,
        [SessionCompletedV2.WireName] = SessionCompletedV2,
    }.ToImmutableDictionary();

    /// <summary>A page of the feed as Charging serves it, the sample at position 41.</summary>
    public static ChargingFeedPage FeedPage { get; } = new(
        [
            new ChargingFeedEntry(
                41,
                JsonSerializer.SerializeToElement<ChargingIntegrationEvent>(
                    SessionCompleted, PublishedLanguage.Json)),
        ],
        "/feeds/charging?after=41&limit=100");

    /// <summary>Every DTO of Billing's API, by its golden file's name.</summary>
    public static ImmutableDictionary<string, object> BillingDtos { get; } =
        new Dictionary<string, object>
        {
            ["place-hold-request"] = new PlaceHoldRequest(HoldId, "drv-7f3a91c2", 50m, "USD"),
            ["capture-payment-request"] = new CapturePaymentRequest(13.60m, "USD"),
            ["hold-response"] =
                new HoldResponse(HoldId, HoldStatus.Captured, 50m, "USD", Captured: 13.60m),
            ["hold-problem"] = new HoldProblem(
                409, HoldProblemCodes.ExceedsHold, "50.01 USD is not within the 50 USD held."),
            ["receipt-response"] = new ReceiptResponse(
                Session,
                "04A2B3C4D5E6F7",
                "OHM-SLC-0042/1",
                30m,
                Started,
                Ended,
                "USD",
                13.60m,
                [
                    new ReceiptLine("energy", 30m, "kWh", 0.42m, 12.60m),
                    new ReceiptLine("session-fee", 1m, "session", 1m, 1m),
                ],
                Ended.AddSeconds(2)),
        }.ToImmutableDictionary();
}
