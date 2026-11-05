using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Contracts.Budgets;

/// <summary>
/// Everything a site's power budget can be asked to do. Every command names its site, which is
/// how the shell routes it; connectors and sessions are referenced by identity only.
/// </summary>
public closed record SitePowerBudgetCommand(SiteId SiteId);

/// <summary>The site's grid connection, safety margin, time zone and connectors.</summary>
public sealed record ConfigureSite(
    SiteId SiteId,
    Power GridLimit,
    Power SafetyMargin,
    string TimeZoneId,
    ImmutableArray<ConnectorLimits> Connectors)
    : SitePowerBudgetCommand(SiteId);

/// <summary>A session on a connector at the site asks for power, up to what it can use.</summary>
public sealed record RequestAllocation(
    SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId, Power Requested)
    : SitePowerBudgetCommand(SiteId);

/// <summary>The session no longer needs power; its share goes back to the others.</summary>
public sealed record ReleaseAllocation(SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId)
    : SitePowerBudgetCommand(SiteId);

/// <summary>
/// The grid operator asks the site to draw less, by this much, until further notice. A new
/// curtailment replaces the one in force.
/// </summary>
public sealed record ApplyCurtailment(SiteId SiteId, Power Reduction)
    : SitePowerBudgetCommand(SiteId);

/// <summary>The curtailment in force is over.</summary>
public sealed record EndCurtailment(SiteId SiteId) : SitePowerBudgetCommand(SiteId);
