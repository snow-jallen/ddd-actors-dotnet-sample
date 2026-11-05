namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// A projection stream gave up on an event under <see cref="StopAfterRetries"/>: the stream's
/// completion fails with this, and its stored offset stays just before the event.
/// </summary>
public sealed class ProjectionStoppedException(
    ProjectionStream stream, long offset, Exception cause)
    : Exception($"Projection {stream} stopped at offset {offset}.", cause)
{
    public ProjectionStream Stream { get; } = stream;

    public long Offset { get; } = offset;
}
