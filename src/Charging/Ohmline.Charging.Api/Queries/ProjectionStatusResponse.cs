namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// How far one projection stream has got. <paramref name="EventsBehind"/> counts the events of
/// its tag stored after <paramref name="StoredOffset"/>: zero for a stream that has caught up,
/// and a number that only grows for one that has stopped. <paramref name="LagMs"/> is how long
/// the last event it handled took from the journal to its read model (section 14.3): from
/// <paramref name="LastEventAt"/>, by the writing node's clock, to <paramref name="UpdatedAt"/>,
/// by the database's. Both are null until the stream has handled an event.
/// </summary>
public sealed record ProjectionStatusResponse(
    string ProjectionId,
    string Tag,
    long StoredOffset,
    long JournalMaxOrdering,
    long EventsBehind,
    long Failures,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastEventAt,
    long? LagMs);
