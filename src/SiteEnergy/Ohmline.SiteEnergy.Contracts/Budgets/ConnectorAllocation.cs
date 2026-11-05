using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Contracts.Budgets;

/// <summary>
/// The power one connector may draw now: a power when its session's request is admitted, zero
/// while it waits, and none once the session has released it.
/// </summary>
public sealed record ConnectorAllocation(ConnectorId ConnectorId, Power? Allocated);
