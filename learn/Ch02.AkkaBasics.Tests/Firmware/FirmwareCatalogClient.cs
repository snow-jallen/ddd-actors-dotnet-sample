using System.Collections.Concurrent;

namespace Ch02.AkkaBasics.Tests.Firmware;

/// <summary>
/// A fake of a vendor's firmware catalog client, the kind of Task-returning I/O an actor calls.
/// A lookup stays pending until the test publishes a release or fails it, so each test decides
/// exactly when the I/O finishes.
/// </summary>
public sealed class FirmwareCatalogClient
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<FirmwareRelease>> _lookups =
        new();
    private int _lookupCount;

    public int LookupCount => Volatile.Read(ref _lookupCount);

    public Task<FirmwareRelease> GetLatestAsync(
        string model, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _lookupCount);
        return LookupFor(model).Task.WaitAsync(cancellationToken);
    }

    /// <summary>Completes the pending lookup; later lookups of the model succeed at once.</summary>
    public void Publish(string model, string version) =>
        LookupFor(model).TrySetResult(new FirmwareRelease(model, version));

    /// <summary>Fails the pending lookup; the next lookup for the model is pending again.</summary>
    public void Fail(string model, Exception exception)
    {
        if (_lookups.TryRemove(model, out var lookup))
            lookup.TrySetException(exception);
    }

    private TaskCompletionSource<FirmwareRelease> LookupFor(string model) =>
        _lookups.GetOrAdd(model, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
}
