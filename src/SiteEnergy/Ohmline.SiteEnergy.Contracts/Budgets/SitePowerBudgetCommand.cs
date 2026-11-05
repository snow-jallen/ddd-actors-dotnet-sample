using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Contracts.Budgets;

/// <summary>
/// Everything a site's power budget can be asked to do. Every command names its site, which is
/// how the shell routes it, and carries the id its sender gave it; connectors and sessions are
/// referenced by identity only.
/// </summary>
public closed record SitePowerBudgetCommand(CommandId CommandId, SiteId SiteId);

/// <summary>The site's grid connection, safety margin, time zone and connectors.</summary>
public sealed record ConfigureSite(
    CommandId CommandId,
    SiteId SiteId,
    Power GridLimit,
    Power SafetyMargin,
    string TimeZoneId,
    ImmutableArray<ConnectorLimits> Connectors)
    : SitePowerBudgetCommand(CommandId, SiteId);

/// <summary>A session on a connector at the site asks for power, up to what it can use.</summary>
public sealed record RequestAllocation(
    CommandId CommandId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    SessionId SessionId,
    Power Requested)
    : SitePowerBudgetCommand(CommandId, SiteId);

/// <summary>The session no longer needs power; its share goes back to the others.</summary>
public sealed record ReleaseAllocation(
    CommandId CommandId, SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId)
    : SitePowerBudgetCommand(CommandId, SiteId);

/// <summary>
/// The grid operator asks the site to draw less, by this much, until further notice. A new
/// curtailment replaces the one in force.
/// </summary>
public sealed record ApplyCurtailment(CommandId CommandId, SiteId SiteId, Power Reduction)
    : SitePowerBudgetCommand(CommandId, SiteId);

/// <summary>The curtailment in force is over.</summary>
public sealed record EndCurtailment(CommandId CommandId, SiteId SiteId)
    : SitePowerBudgetCommand(CommandId, SiteId);
