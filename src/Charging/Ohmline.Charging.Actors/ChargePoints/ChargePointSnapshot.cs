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
/// <remarks>
/// Its second version (section 20.6) records when it was taken, and each reservation as how long
/// it had left then: it reads as the charge point stood at that moment, which is what operators
/// look for in it. The first version is read through an upcaster.
/// </remarks>
public sealed record ChargePointSnapshot(
    ChargePointId ChargePointId,
    SiteId SiteId,
    DateTimeOffset TakenAt,
    ImmutableArray<ConnectorSnapshot> Connectors)
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
        && TakenAt == other.TakenAt
        && Connectors.SequenceEqual(other.Connectors)
        && RecentCommands.SequenceEqual(other.RecentCommands);

    public override int GetHashCode() =>
        HashCode.Combine(ChargePointId, SiteId, Connectors.Length);
}
