using System.Collections.Immutable;
using System.Text.Json;
using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.Billing.Feed;

/// <summary>
/// Billing's tolerant reader of Charging's Published Language (section 16.3): it reads the
/// types it bills for and steps over every other, so a new kind of event, or a new version of
/// one, never stops Billing. Pure, so the contract tests can feed it Charging's golden files
/// (section 16.10).
/// </summary>
public static class ChargingFeedReader
{
    /// <summary>
    /// The versions of Charging's events Billing reads: its side of a consumer-driven contract
    /// (section 20.9). Charging's contract tests check that it still publishes every one, so
    /// Charging can stop publishing a version only after Billing has stopped reading it.
    /// </summary>
    public static IReadOnlyList<string> WireNamesRead { get; } = [SessionCompleted.WireName];

    public static ImmutableArray<FeedItem> Read(ChargingFeedPage page) =>
        [.. page.Entries.Select(Read)];

    public static FeedItem Read(ChargingFeedEntry entry) => entry.Type switch
    {
        SessionCompleted.WireName => new CompletedSession(
            entry.Position,
            entry.Event.Deserialize<SessionCompleted>(PublishedLanguage.Json)
                ?? throw new JsonException($"Entry {entry.Position} has no event.")),
        var other => new SkippedEntry(entry.Position, other),
    };
}
