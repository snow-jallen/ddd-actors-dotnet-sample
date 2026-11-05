using System.Collections.Immutable;
using System.Text.Json;

namespace Ohmline.Charging.Contracts.Integration;

/// <summary>
/// A page of the charging feed (section 16.2): the integration events after a position, oldest
/// first, and the link to the page after this one. A consumer stores the position of the last
/// event it has handled and follows <see cref="Next"/>; at the end of the feed the link points
/// at the same place again, and stays empty until something new is published.
/// </summary>
public sealed record ChargingFeedPage(ImmutableArray<ChargingFeedEntry> Entries, string Next)
{
    /// <summary>Where the feed is served, by every node that has the read models.</summary>
    public const string Path = "/feeds/charging";

    public bool Equals(ChargingFeedPage? other) =>
        other is not null && Next == other.Next && Entries.SequenceEqual(other.Entries);

    public override int GetHashCode() => HashCode.Combine(Next, Entries.Length);
}

/// <summary>
/// One event of the feed at its position. The event stays JSON, with its <c>type</c>: a reader
/// deserializes the types it knows and steps over the rest, so a new kind of event never breaks
/// an old reader (section 16.10).
/// </summary>
public sealed record ChargingFeedEntry(long Position, JsonElement Event)
{
    /// <summary>The wire name, such as <c>ohmline.charging.session-completed.v1</c>.</summary>
    public string? Type =>
        Event.ValueKind is JsonValueKind.Object
        && Event.TryGetProperty("type", out var type)
        && type.ValueKind is JsonValueKind.String
            ? type.GetString()
            : null;
}
