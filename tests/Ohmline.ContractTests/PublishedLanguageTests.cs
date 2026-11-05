using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ohmline.Charging.Contracts.Integration;
using Ohmline.Testing;

namespace Ohmline.ContractTests;

/// <summary>
/// Charging's side of the contract: every version of every integration event is written exactly
/// as its golden file says, and the golden file reads back as the event. A changed field fails
/// here; the fix is a new version (<c>.v2</c>) beside the old one, never an edit of the old one.
/// </summary>
public sealed class PublishedLanguageTests
{
    public static TheoryData<string> WireNames => new(Samples.IntegrationEvents.Keys);

    [Fact]
    public void Every_wire_name_Charging_publishes_has_a_sample_and_a_golden_file()
    {
        var published = typeof(ChargingIntegrationEvent)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(derived => (string)derived.TypeDiscriminator!);

        published.ShouldBe(Samples.IntegrationEvents.Keys, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(WireNames))]
    public void An_integration_event_is_written_as_its_golden_file_says(string wireName)
    {
        var json = JsonSerializer.Serialize(
            Samples.IntegrationEvents[wireName], PublishedLanguage.Json);

        Golden.Verify($"published-language/{wireName}", json);
    }

    [Theory]
    [MemberData(nameof(WireNames))]
    public void The_golden_file_reads_back_as_the_event(string wireName)
    {
        var json = Golden.Read($"published-language/{wireName}");

        JsonSerializer.Deserialize<ChargingIntegrationEvent>(json, PublishedLanguage.Json)
            .ShouldBe(Samples.IntegrationEvents[wireName]);
    }

    [Fact]
    public void A_page_of_the_feed_is_written_as_its_golden_file_says()
    {
        Golden.Verify(
            "published-language/charging-feed-page",
            JsonSerializer.Serialize(Samples.FeedPage, JsonSerializerOptions.Web));
    }
}
