using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>
/// Everything that can happen to a site's power budget. Every allocation a decision produced is
/// recorded, never recomputed on replay: a new version of the allocator must not change what
/// the chargers were already told. Every event records the command that caused it and when it
/// happened; <see cref="SitePowerBudget"/> stamps both on every event of a decision.
/// </summary>
public closed record SitePowerBudgetEvent(SiteId SiteId)
{
    /// <summary>The command that caused this event: its causation.</summary>
    public CommandId CommandId { get; init; }

    /// <summary>When it happened: the time of the decision, from its context.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>The site's configuration, checked and accepted.</summary>
public sealed record SiteConfigured(SiteId SiteId, SiteConfiguration Configuration)
    : SitePowerBudgetEvent(SiteId);

/// <summary>A session's request was admitted, with all or part of what it asked for.</summary>
public sealed record AllocationGranted(
    SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId, Power Requested, Power Granted)
    : SitePowerBudgetEvent(SiteId);

/// <summary>A session's request is open, but its minimum does not fit now.</summary>
public sealed record AllocationQueued(
    SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId, Power Requested)
    : SitePowerBudgetEvent(SiteId);

/// <summary>
/// Every allocation at the site after a change, in request order; a request that is not in the
/// table is queued. Equal when the tables are equal: a record alone would compare the array by
/// reference.
/// </summary>
public sealed record AllocationsRebalanced(
    SiteId SiteId, ImmutableArray<PowerAllocation> Allocations)
    : SitePowerBudgetEvent(SiteId)
{
    public bool Equals(AllocationsRebalanced? other) =>
        other is not null && base.Equals(other) && Allocations.SequenceEqual(other.Allocations);

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Allocations.Length);
}

public sealed record AllocationReleased(SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId)
    : SitePowerBudgetEvent(SiteId);

/// <summary>The site must draw this much less than its configured capacity.</summary>
public sealed record CurtailmentApplied(SiteId SiteId, Power Reduction)
    : SitePowerBudgetEvent(SiteId);

public sealed record CurtailmentEnded(SiteId SiteId) : SitePowerBudgetEvent(SiteId);
