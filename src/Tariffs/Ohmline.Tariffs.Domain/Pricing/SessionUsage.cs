using System.Collections.Immutable;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Tariffs.Domain.Pricing;

/// <summary>
/// What a session used, as pricing needs it: the energy metered between consecutive readings,
/// and how long the car stayed plugged in after it stopped charging. Equal when the values are
/// equal: a record alone would compare the intervals by reference.
/// </summary>
public sealed record SessionUsage(ImmutableArray<MeteredInterval> Intervals, TimeSpan Idle)
{
    public Energy Energy =>
        Intervals.Aggregate(Energy.Zero, (total, interval) => total + interval.Energy);

    public bool Equals(SessionUsage? other) =>
        other is not null && Idle == other.Idle && Intervals.SequenceEqual(other.Intervals);

    public override int GetHashCode() => HashCode.Combine(Idle, Intervals.Length);
}
