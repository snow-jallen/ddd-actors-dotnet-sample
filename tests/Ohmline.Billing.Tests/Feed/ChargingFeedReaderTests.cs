using System.Collections.Immutable;
using System.Text.Json;
using Ohmline.Billing.Feed;
using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.Billing.Tests.Feed;

/// <summary>Billing's tolerant reader: what it bills for, and what it steps over.</summary>
public sealed class ChargingFeedReaderTests
{
    [Fact]
    public void Given_a_completed_session_then_it_is_read_with_its_price()
    {
        var completed = Known.Completed();

        var item = ChargingFeedReader.Read(Entry(7, completed));

        item.Value.ShouldBe(new CompletedSession(7, completed));
    }

    [Fact]
    public void Given_an_event_of_a_type_Billing_does_not_know_then_it_is_stepped_over()
    {
        var unknown = JsonSerializer.SerializeToElement(
            new { type = "ohmline.charging.session-completed.v2", energyWh = 30_000 });

        var item = ChargingFeedReader.Read(new ChargingFeedEntry(8, unknown));

        item.Value.ShouldBe(new SkippedEntry(8, "ohmline.charging.session-completed.v2"));
        item.Position.ShouldBe(8);
    }

    [Fact]
    public void Given_fields_Billing_does_not_know_then_they_are_ignored()
    {
        var completed = Known.Completed();
        var json = JsonSerializer.SerializeToNode(
            (ChargingIntegrationEvent)completed, PublishedLanguage.Json)!.AsObject();
        json["roamingPartner"] = "NL-ABC";

        var item = ChargingFeedReader.Read(
            new ChargingFeedEntry(9, JsonSerializer.SerializeToElement(json)));

        item.Value.ShouldBe(new CompletedSession(9, completed));
    }

    [Fact]
    public void A_page_is_read_entry_by_entry_in_order()
    {
        var page = new ChargingFeedPage(
            [Entry(1, Known.Completed()), Entry(2, Known.Completed())],
            "/feeds/charging?after=2&limit=100");

        ChargingFeedReader.Read(page).Select(item => item.Position).ShouldBe([1L, 2L]);
    }

    private static ChargingFeedEntry Entry(long position, ChargingIntegrationEvent @event) =>
        new(position, JsonSerializer.SerializeToElement(@event, PublishedLanguage.Json));
}
