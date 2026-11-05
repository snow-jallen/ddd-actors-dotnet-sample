using System.Collections.Immutable;
using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Domain.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Actors.Budgets;

/// <summary>
/// A configured site's power budget, as its snapshot stores it: the configuration, the
/// curtailment in force, and every open request and allocation, in order. A DTO with a manifest
/// of its own, never the state union. Equal when the values are equal.
/// </summary>
public sealed record SitePowerBudgetSnapshot(
    SiteConfiguration Site,
    Power Curtailment,
    ImmutableArray<OpenRequest> Requests,
    ImmutableArray<PowerAllocation> Allocations)
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

    public bool Equals(SitePowerBudgetSnapshot? other) =>
        other is not null
        && Site == other.Site
        && Curtailment == other.Curtailment
        && Requests.SequenceEqual(other.Requests)
        && Allocations.SequenceEqual(other.Allocations)
        && RecentCommands.SequenceEqual(other.RecentCommands);

    public override int GetHashCode() =>
        HashCode.Combine(Site, Curtailment, Requests.Length, Allocations.Length);
}
