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

    public void Dispose() => _listener.Dispose();
}
