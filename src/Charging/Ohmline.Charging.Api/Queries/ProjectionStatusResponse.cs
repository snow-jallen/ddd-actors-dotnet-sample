namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// How far one projection stream has got. <paramref name="EventsBehind"/> counts the events of
/// its tag stored after <paramref name="StoredOffset"/>: zero for a stream that has caught up,
/// and a number that only grows for one that has stopped.
/// </summary>
public sealed record ProjectionStatusResponse(
    string ProjectionId,
    string Tag,
    long StoredOffset,
    long JournalMaxOrdering,
    long EventsBehind,
    long Failures,
    DateTimeOffset UpdatedAt);
