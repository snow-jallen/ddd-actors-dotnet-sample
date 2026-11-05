using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Contracts.Budgets;

/// <summary>
/// The answer to <see cref="GetSitePowerBudget"/>: the site's power as the budget shares it out
/// now, at <paramref name="Version"/>. A site that is not configured has version 0, no capacity
/// and no requests. Equal when the values are equal.
/// </summary>
public sealed record SitePowerBudgetView(
    SiteId SiteId,
    long Version,
    Power EffectiveCapacity,
    Power Curtailment,
    ImmutableArray<RequestView> Requests)
{
    public bool Equals(SitePowerBudgetView? other) =>
        other is not null
        && SiteId == other.SiteId
        && Version == other.Version
        && EffectiveCapacity == other.EffectiveCapacity
        && Curtailment == other.Curtailment
        && Requests.SequenceEqual(other.Requests);

    public override int GetHashCode() =>
        HashCode.Combine(SiteId, Version, EffectiveCapacity, Curtailment, Requests.Length);
}

/// <summary>
/// One session's open request, in arrival order: the power it asked for and the power it is
/// allocated now, or none while it waits in the queue.
/// </summary>
public sealed record RequestView(
    ConnectorId ConnectorId, SessionId SessionId, Power Requested, Power? Allocated);
