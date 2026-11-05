using Akka.Persistence;
using Akka.Persistence.TestKit;

namespace Ch10.PersistenceLab.Tests;

/// <summary>
/// A journal write that waits until the test releases it. Every write waits for the same
/// release, so the first one holds the journal. Install it with
/// <c>Journal.OnWrite.SetInterceptorAsync</c>.
/// </summary>
public sealed class HeldJournal : IJournalInterceptor
{
    private readonly TaskCompletionSource _reached =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _released =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Reached => _reached.Task;

    public void Release() => _released.TrySetResult();

    public async Task InterceptAsync(IPersistentRepresentation message)
    {
        _reached.TrySetResult();
        await _released.Task;
    }
}
