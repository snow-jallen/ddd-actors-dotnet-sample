using System.Collections.Immutable;
using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// A registered charge point, as its snapshot stores it. A DTO with a manifest of its own, never
/// the state union: the state may change shape in memory without breaking a stored snapshot, and
/// when the snapshot itself must change, it gets a new manifest version. A snapshot is a cache of
/// the fold of the events before it, never a replacement for them. Equal when the values are
/// equal: a record alone would compare the array by reference.
/// </summary>
public sealed record ChargePointSnapshot(
    ChargePointId ChargePointId, SiteId SiteId, ImmutableArray<ConnectorSnapshot> Connectors)
{
    /// <summary>
    /// The commands it accepted most recently, so that a duplicate after a restart still gets
    /// its first answer (section 12.6). A snapshot stored before there was a window has none.
    /// </summary>
    public ImmutableArray<RecentCommand> RecentCommands
    {
        get;
        init => field = value.IsDefault ? [] : value;
    } = [];

    public bool Equals(ChargePointSnapshot? other) =>
        other is not null
        && ChargePointId == other.ChargePointId
        && SiteId == other.SiteId
        && Connectors.SequenceEqual(other.Connectors)
        && RecentCommands.SequenceEqual(other.RecentCommands);

    public override int GetHashCode() =>
        HashCode.Combine(ChargePointId, SiteId, Connectors.Length);
}
