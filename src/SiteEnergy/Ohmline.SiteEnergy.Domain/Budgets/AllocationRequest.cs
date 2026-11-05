using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>
/// One connector's claim on the site's capacity, as the allocator sees it: at least its minimum,
/// or nothing at all; at most its maximum; and served in the order of its priority class.
/// </summary>
public sealed record AllocationRequest(
    ConnectorId ConnectorId, Power MinPower, Power MaxPower, PriorityClass Priority)
{
    /// <summary>
    /// What a session asks for, bounded by what its connector can do. A request below the
    /// connector's minimum still needs the minimum: the charger cannot deliver less.
    /// </summary>
    public static AllocationRequest For(ConnectorConfiguration connector, Power requested) =>
        new(connector.ConnectorId,
            connector.MinPower,
            requested < connector.MinPower ? connector.MinPower
                : requested > connector.MaxPower ? connector.MaxPower
                : requested,
            connector.Priority);
}
