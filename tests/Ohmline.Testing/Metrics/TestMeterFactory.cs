using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Ohmline.Testing.Metrics;

/// <summary>
/// A meter factory of the test's own, as a host's would be: every meter it makes has it as its
/// scope, so a test can listen to its own meters and to nobody else's, while other tests run
/// in parallel in the same process with meters of the same names (section 18.1).
/// </summary>
public sealed class TestMeterFactory : IMeterFactory
{
    private readonly ConcurrentDictionary<string, Meter> _meters = new();

    /// <summary>One meter per name, as the host's factory caches them.</summary>
    public Meter Create(MeterOptions options) =>
        _meters.GetOrAdd(
            options.Name,
            name => new Meter(new MeterOptions(name) { Version = options.Version, Scope = this }));

    public void Dispose()
    {
        foreach (var meter in _meters.Values)
        {
            meter.Dispose();
        }
    }
}
