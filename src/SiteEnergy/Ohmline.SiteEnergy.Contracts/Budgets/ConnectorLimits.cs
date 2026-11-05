using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Contracts.Budgets;

/// <summary>
/// One connector as a site configuration describes it: the power it can draw, from the minimum
/// below which charging stops to its maximum, and its priority class. Plain data on the wire;
/// the site power budget checks it before it accepts a configuration.
/// </summary>
public sealed record ConnectorLimits(
    ConnectorId ConnectorId, Power MinPower, Power MaxPower, PriorityClass Priority);
