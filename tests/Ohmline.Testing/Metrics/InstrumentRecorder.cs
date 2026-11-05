using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;

namespace Ohmline.Testing.Metrics;

/// <summary>
/// What one meter factory's instruments record, by hand, with the BCL's
/// <see cref="MeterListener"/>: a listener hears every meter in the process, so it enables only
/// the instruments whose meter the test's factory made (section 18.1).
/// </summary>
public sealed class InstrumentRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<Recorded> _recorded = new();

    public InstrumentRecorder(IMeterFactory meters)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, meters))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _recorded.Enqueue(new(instrument.Name, value, Tags(tags))));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            _recorded.Enqueue(new(instrument.Name, value, Tags(tags))));
        _listener.Start();
    }

    /// <summary>Every measurement of the instrument so far, oldest first.</summary>
    public IReadOnlyList<Recorded> Of(string instrument) =>
        [.. _recorded.Where(recorded => recorded.Instrument == instrument)];

    /// <summary>Asks each observable instrument for its value now, as a collection would.</summary>
    public void Observe() => _listener.RecordObservableInstruments();

    public void Dispose() => _listener.Dispose();

    private static ImmutableDictionary<string, string?> Tags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string?>();
        foreach (var (key, value) in tags)
        {
            builder[key] = value?.ToString();
        }

        return builder.ToImmutable();
    }

    /// <summary>One measurement: the instrument, the value and its tags.</summary>
    public sealed record Recorded(
        string Instrument, double Value, ImmutableDictionary<string, string?> Tags);
}
