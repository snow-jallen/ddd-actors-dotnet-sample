using System.Collections.Immutable;
using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Listings;

/// <summary>
/// A listing as its snapshot stores it. The listing is state sourced, so once its events are
/// deleted this row is the only copy of it: the DTO and its manifest are as permanent as an
/// event's. Equal when the values are equal.
/// </summary>
public sealed record ChargePointListingSnapshot(
    ChargePointId ChargePointId,
    string DisplayName,
    string AccessInstructions,
    string OpeningHours,
    long Revision)
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

    public bool Equals(ChargePointListingSnapshot? other) =>
        other is not null
        && ChargePointId == other.ChargePointId
        && DisplayName == other.DisplayName
        && AccessInstructions == other.AccessInstructions
        && OpeningHours == other.OpeningHours
        && Revision == other.Revision
        && RecentCommands.SequenceEqual(other.RecentCommands);

    public override int GetHashCode() => HashCode.Combine(ChargePointId, Revision);
}
