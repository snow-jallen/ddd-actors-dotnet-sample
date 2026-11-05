using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>The power one connector's session may draw now. Replaced, never changed.</summary>
public readonly record struct PowerAllocation(ConnectorId ConnectorId, Power Power)
{
    public override string ToString() => $"{ConnectorId}: {Power}";
}
