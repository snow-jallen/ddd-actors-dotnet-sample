using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>
/// What the allocator's answer means for one request. In process only: the site power budget
/// turns it into an <see cref="AllocationGranted"/> or an <see cref="AllocationQueued"/> event.
/// </summary>
public union AllocationDecision(Granted, Reduced, Queued)
{
    public static AllocationDecision Of(
        AllocationRequest request, ImmutableArray<PowerAllocation> allocations) =>
        allocations.Where(allocation => allocation.ConnectorId == request.ConnectorId).ToArray()
            switch
            {
                [] => new Queued(request.ConnectorId),
                [var allocation] when allocation.Power >= request.MaxPower =>
                    new Granted(allocation),
                [var allocation] => new Reduced(allocation, request.MaxPower),
                [_, _, ..] => throw new InvalidOperationException(
                    $"Connector {request.ConnectorId} has more than one allocation."),
            };
}

/// <summary>Admitted with all the power it asked for.</summary>
public sealed record Granted(PowerAllocation Allocation);

/// <summary>Admitted, with less power than it asked for.</summary>
public sealed record Reduced(PowerAllocation Allocation, Power Requested);

/// <summary>Not admitted now: its minimum does not fit. It waits for a rebalance.</summary>
public sealed record Queued(ConnectorId ConnectorId);
