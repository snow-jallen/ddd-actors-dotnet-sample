using System.Text.Json;
using System.Text.Json.Nodes;
using Ohmline.Billing.Contracts.Receipts;
using Ohmline.Billing.Feed;
using Ohmline.Billing.Invoicing;
using Ohmline.Charging.Contracts.Integration;
using Ohmline.Testing;

namespace Ohmline.ContractTests;

/// <summary>
/// The consumer's side (section 16.10): Billing's own reader, run over the page of the feed that
/// Charging's golden file says it writes. What Billing needs from a completed session, and what
/// it does with what it does not know, checked against Charging's contract, not against a copy.
/// </summary>
public sealed class BillingReadsTheFeedTests
{
    [Fact]
    public void Given_the_golden_page_then_Billing_reads_the_completed_session_and_its_price()
    {
        var item = ChargingFeedReader.Read(GoldenPage()).ShouldHaveSingleItem();

        item.Value.ShouldBe(new CompletedSession(41, Samples.SessionCompleted));
    }

    [Fact]
    public void Given_the_golden_session_then_Billing_issues_the_receipt_drivers_expect()
    {
        var completed = ChargingFeedReader.Read(GoldenPage()).ShouldHaveSingleItem()
            .Value.ShouldBeOfType<CompletedSession>().Event;

        var receipt = Invoice.From(completed, Samples.Ended.AddSeconds(2)).ToReceipt();

        receipt.ShouldBe((ReceiptResponse)Samples.BillingDtos["receipt-response"]);
    }

    [Fact]
    public void Given_a_version_and_a_kind_of_event_Billing_does_not_know_then_it_steps_over_them()
    {
        var page = GoldenJson();
        var entries = page["entries"]!.AsArray();
        var v2 = entries[0]!.DeepClone();
        v2["position"] = 42;
        v2["event"]!["type"] = "ohmline.charging.session-completed.v2";
        entries.Add(v2);
        entries.Add(new JsonObject
        {
            ["position"] = 43,
            ["event"] = new JsonObject { ["type"] = "ohmline.charging.tariff-changed.v1" },
        });

        var items = ChargingFeedReader.Read(Read(page.ToJsonString()));

        items.Select(item => item.Value.GetType().Name).ShouldBe(
            [nameof(CompletedSession), nameof(SkippedEntry), nameof(SkippedEntry)]);
        items.Select(item => item.Position).ShouldBe([41L, 42L, 43L]);
    }

    [Fact]
    public void Given_fields_Billing_does_not_know_then_the_session_reads_the_same()
    {
        var page = GoldenJson();
        page["entries"]![0]!["event"]!["roamingPartner"] = "NL-ABC";
        page["entries"]![0]!["event"]!["price"]!["vatIncluded"] = true;

        var item = ChargingFeedReader.Read(Read(page.ToJsonString())).ShouldHaveSingleItem();

        item.Value.ShouldBe(new CompletedSession(41, Samples.SessionCompleted));
    }

    private static ChargingFeedPage GoldenPage() =>
        Read(Golden.Read("published-language/charging-feed-page"));

    private static JsonObject GoldenJson() =>
        JsonNode.Parse(Golden.Read("published-language/charging-feed-page"))!.AsObject();

    /// <summary>As Billing's feed client reads a page off the wire.</summary>
    private static ChargingFeedPage Read(string json) =>
        JsonSerializer.Deserialize<ChargingFeedPage>(json, PublishedLanguage.Json)!;
}
