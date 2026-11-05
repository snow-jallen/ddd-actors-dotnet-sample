using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>
/// Everything the charge point remembers in order to decide. Before registration there is
/// nothing to remember, so <see cref="Unregistered"/> carries no data at all.
/// </summary>
public union ChargePointState(Unregistered, Registered)
{
    /// <summary>Where every charge point starts, and where recovery starts its fold.</summary>
    public static ChargePointState Initial { get; } = new Unregistered();
}

/// <summary>Not yet known to Ohmline.</summary>
public sealed record Unregistered;

/// <summary>
/// A charge point on the network: its site, by identity, and its connectors by number. Equal
/// when the values are equal: a record alone would compare the dictionary by reference.
/// </summary>
public sealed record Registered(
    ChargePointId ChargePointId, SiteId SiteId, ImmutableDictionary<int, Connector> Connectors)
{
    public bool Equals(Registered? other) =>
        other is not null
        && ChargePointId == other.ChargePointId
        && SiteId == other.SiteId
        && Connectors.Count == other.Connectors.Count
        && Connectors.All(pair =>
            other.Connectors.TryGetValue(pair.Key, out var connector) && connector == pair.Value);

    public override int GetHashCode() =>
        HashCode.Combine(ChargePointId, SiteId, Connectors.Count);
}
