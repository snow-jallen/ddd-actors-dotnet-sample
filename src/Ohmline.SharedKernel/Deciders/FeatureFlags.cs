using System.Collections.Frozen;

namespace Ohmline.SharedKernel.Deciders;

/// <summary>
/// The feature flags that were on when a command arrived, as the shell evaluated them (section
/// 21.8). A decider may let one change what it decides, and then records what it decided in
/// the event; it never reads a flag in Evolve, so replay gives the same state whatever the flags
/// say on the day of the replay.
/// </summary>
public sealed class FeatureFlags
{
    /// <summary>No flag on: what a decider sees unless the shell evaluated some.</summary>
    public static readonly FeatureFlags None = new([]);

    private readonly FrozenSet<string> _enabled;

    public FeatureFlags(IEnumerable<string> enabled) =>
        _enabled = enabled.ToFrozenSet(StringComparer.Ordinal);

    public bool IsEnabled(string feature) => _enabled.Contains(feature);

    public override string ToString() =>
        _enabled.Count == 0 ? "none" : string.Join(", ", _enabled.Order(StringComparer.Ordinal));
}
