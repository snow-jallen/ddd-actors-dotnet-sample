using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>What a site's power budget remembers in order to decide.</summary>
public union SitePowerBudgetState(Unconfigured, Configured)
{
    public static SitePowerBudgetState Initial { get; } = new Unconfigured();
}

/// <summary>Nothing is known about the site yet.</summary>
public sealed record Unconfigured;

/// <summary>
/// The site, the curtailment in force (zero when there is none), every open request in arrival
/// order, and the allocation of each admitted one. Equal when the values are equal.
/// </summary>
public sealed record Configured(
    SiteConfiguration Site,
    Power Curtailment,
    ImmutableArray<OpenRequest> Requests,
    ImmutableArray<PowerAllocation> Allocations)
{
    public static Configured Of(SiteConfiguration site) => new(site, Power.Zero, [], []);

    /// <summary>
    /// Grid connection limit minus active curtailment minus safety margin; never below zero,
    /// however deep the curtailment.
    /// </summary>
    public Power EffectiveCapacity =>
        Site.ConfiguredCapacity > Curtailment ? Site.ConfiguredCapacity - Curtailment : Power.Zero;

    public Power Allocated =>
        Allocations.Aggregate(Power.Zero, (sum, allocation) => sum + allocation.Power);

    /// <summary>
    /// The budget's invariant, stated once as an assertion: the allocations add up to no more
    /// than the effective capacity, and each belongs to one open request and lies between its
    /// connector's minimum and what the request may draw. Every decision keeps it true, and the
    /// property tests check it after every decision of many random scripts (section 9.4).
    /// </summary>
    public bool AllocationsFit =>
        Allocated <= EffectiveCapacity
        && Allocations.Select(allocation => allocation.ConnectorId).Distinct().Count()
            == Allocations.Length
        && Allocations.All(allocation =>
            AllocationRequests.SingleOrDefault(
                    request => request.ConnectorId == allocation.ConnectorId) is { } request
            && allocation.Power >= request.MinPower
            && allocation.Power <= request.MaxPower);

    /// <summary>The open requests as the allocator sees them, bounded by connectors.</summary>
    public ImmutableArray<AllocationRequest> AllocationRequests =>
    [
        .. from request in Requests
           join connector in Site.Connectors on request.ConnectorId equals connector.ConnectorId
           select AllocationRequest.For(connector, request.Requested),
    ];

    public OpenRequest? RequestFor(ConnectorId connectorId) =>
        Requests.FirstOrDefault(request => request.ConnectorId == connectorId);

    public Configured Open(OpenRequest request, Power? granted) => this with
    {
        Requests = Requests.Add(request),
        Allocations = granted is { } power
            ? Allocations.Add(new PowerAllocation(request.ConnectorId, power))
            : Allocations,
    };

    public Configured Close(ConnectorId connectorId) => this with
    {
        Requests = Requests.RemoveAll(request => request.ConnectorId == connectorId),
        Allocations = Allocations.RemoveAll(allocation => allocation.ConnectorId == connectorId),
    };

    public bool Equals(Configured? other) =>
        other is not null
        && Site == other.Site
        && Curtailment == other.Curtailment
        && Requests.SequenceEqual(other.Requests)
        && Allocations.SequenceEqual(other.Allocations);

    public override int GetHashCode() =>
        HashCode.Combine(Site, Curtailment, Requests.Length, Allocations.Length);
}
