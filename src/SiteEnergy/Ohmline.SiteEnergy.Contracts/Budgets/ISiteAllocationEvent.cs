using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.SiteEnergy.Contracts.Budgets;

/// <summary>
/// What a Site Energy event says about allocations, for readers outside Site Energy: the
/// published view of its events under the <c>site-energy</c> tag (section 13.8). Site Energy's
/// allocation events implement it; a reader such as Charging's site live board matches on this
/// interface and never on Site Energy's own event types.
/// </summary>
public interface ISiteAllocationEvent
{
    SiteId SiteId { get; }

    /// <summary>The connectors whose allocation the event sets.</summary>
    ImmutableArray<ConnectorAllocation> Allocations { get; }

    /// <summary>
    /// True when <see cref="Allocations"/> is every allocation at the site after the event: any
    /// other connector that has asked for power now waits, with nothing allocated.
    /// </summary>
    bool CoversWholeSite { get; }
}
