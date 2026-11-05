using Akka.Streams;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>A projection stream that is running: stop it with its kill switch.</summary>
public sealed class RunningProjection(
    ProjectionStream stream, UniqueKillSwitch killSwitch, Task completion) : IAsyncDisposable
{
    public ProjectionStream Stream { get; } = stream;

    /// <summary>
    /// Completes when the stream stops: after <see cref="DisposeAsync"/>, or when it gives up.
    /// </summary>
    public Task Completion { get; } = completion;

    /// <summary>
    /// Stops reading. The event being handled, if any, finishes or rolls back; either way the
    /// stored offset says where the next start resumes.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        killSwitch.Shutdown();
        await Completion.ContinueWith(_ => { }, TaskScheduler.Default);
    }
}
