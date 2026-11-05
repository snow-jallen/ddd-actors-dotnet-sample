using System.Collections.Concurrent;
using Akka.Persistence;
using Akka.Persistence.TestKit;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

/// <summary>
/// A journal that is slow on cue: the write of a <typeparamref name="TEvent"/> waits until the
/// test releases it, and every other write passes. Install it with
/// <c>Journal.OnWrite.SetInterceptorAsync</c>; the test journal calls it once per event.
/// </summary>
/// <remarks>
/// With <c>serialize-messages = on</c> the interceptor reaches the journal as a serialized copy,
/// not as this object. So the copy carries only an id, and both find the same gate by it.
/// </remarks>
internal sealed class HeldWrite<TEvent> : IJournalInterceptor
{
    private static readonly ConcurrentDictionary<Guid, Gate> Gates = new();

    public HeldWrite(Guid id) => Id = id;

    public Guid Id { get; }

    /// <summary>Completes when the journal has begun writing the held event.</summary>
    public Task Reached => GateOf(Id).Reached.Task;

    public static HeldWrite<TEvent> Create() => new(Guid.NewGuid());

    public void Release() => GateOf(Id).Released.TrySetResult();

    public async Task InterceptAsync(IPersistentRepresentation message)
    {
        if (message.Payload is TEvent)
        {
            var gate = GateOf(Id);
            gate.Reached.TrySetResult();
            await gate.Released.Task;
        }
    }

    private static Gate GateOf(Guid id) => Gates.GetOrAdd(id, _ => new Gate());

    private sealed class Gate
    {
        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
