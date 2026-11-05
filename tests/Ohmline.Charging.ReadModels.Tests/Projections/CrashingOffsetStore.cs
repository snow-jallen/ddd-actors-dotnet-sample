using Ohmline.Charging.ReadModels.Projections;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// The process dying at the worst moment: the projection's write is committed, and the offset
/// save that should follow it never happens, once, at <paramref name="crashAt"/>.
/// </summary>
internal sealed class CrashingOffsetStore(IOffsetStore inner, long crashAt) : IOffsetStore
{
    private bool _crashed;

    public Task<long> LoadAsync(ProjectionStream stream, CancellationToken cancellationToken) =>
        inner.LoadAsync(stream, cancellationToken);

    public Task SaveAsync(
        ProjectionStream stream, long offset, CancellationToken cancellationToken)
    {
        if (offset == crashAt && !_crashed)
        {
            _crashed = true;
            throw new IOException($"Crashed before storing offset {offset}.");
        }

        return inner.SaveAsync(stream, offset, cancellationToken);
    }
}
