using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// Listens to the named activity sources, samples everything they start, and keeps each span
/// once it has stopped. The BCL's own listener, so no SDK is involved: what it records is what
/// any OpenTelemetry exporter would be handed. Listeners are process-wide, and tests run in
/// parallel, so a test reads only the spans of its own trace.
/// </summary>
public sealed class SpanRecorder : IDisposable
{
    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly ActivityListener _listener;

    public SpanRecorder(params IEnumerable<string> sources)
    {
        var names = sources.ToHashSet(StringComparer.Ordinal);
        _listener = new ActivityListener
        {
            ShouldListenTo = source => names.Contains(source.Name),
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>The spans of one trace that have stopped so far, in the order they did.</summary>
    public IReadOnlyList<Activity> Trace(ActivityTraceId traceId) =>
        [.. _stopped.Where(span => span.TraceId == traceId)];

    /// <summary>
    /// The one span of <paramref name="source"/> in the trace, once it has stopped: a reply can
    /// reach the test before the span it was sent from has ended.
    /// </summary>
    public async Task<Activity> SingleAsync(ActivityTraceId traceId, string source)
    {
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var spans = Trace(traceId).Where(span => span.Source.Name == source).ToList();
            if (spans is [_] || patience.IsCancellationRequested)
            {
                return spans.ShouldHaveSingleItem();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
        }
    }

    /// <summary>
    /// The trace's spans, once at least <paramref name="count"/> of them are named
    /// <paramref name="name"/>, or after five seconds, whichever comes first.
    /// </summary>
    public async Task<IReadOnlyList<Activity>> WaitForAsync(
        ActivityTraceId traceId, string name, int count)
    {
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var spans = Trace(traceId);
            if (spans.Count(span => span.DisplayName == name) >= count
                || patience.IsCancellationRequested)
            {
                return spans;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
        }
    }

    /// <summary>Every span that has stopped so far, whatever its trace.</summary>
    public IReadOnlyList<Activity> All => [.. _stopped];

    /// <summary>Every span that ended in an error, whatever its trace.</summary>
    public IReadOnlyList<Activity> Failed =>
        [.. _stopped.Where(span => span.Status == ActivityStatusCode.Error)];

    public void Dispose() => _listener.Dispose();
}
