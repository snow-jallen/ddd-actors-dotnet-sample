using System.Collections.Concurrent;

namespace Ch02.ActorsByHand;

/// <summary>
/// Finds the one actor for an identity, such as a connector id, and creates it on first use.
/// Callers never construct actors themselves, so there is never a second actor for one id.
/// </summary>
public sealed class ActorDirectory<TActor>(Func<string, TActor> createActor) : IAsyncDisposable
    where TActor : IAsyncDisposable
{
    // GetOrAdd may run its factory twice under contention and keep only one result. Storing a
    // Lazy means a losing call creates a cheap wrapper, never a second running actor.
    private readonly ConcurrentDictionary<string, Lazy<TActor>> _actors = new();

    public TActor GetOrCreate(string id) =>
        _actors.GetOrAdd(id, key => new Lazy<TActor>(() => createActor(key))).Value;

    public async ValueTask DisposeAsync()
    {
        foreach (var actor in _actors.Values.Where(actor => actor.IsValueCreated))
            await actor.Value.DisposeAsync();
    }
}
