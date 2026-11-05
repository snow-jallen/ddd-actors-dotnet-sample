using System.Collections.Immutable;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>
/// Shares a site's capacity between requests for power: Site Energy's Cohesive Mechanism. It
/// knows nothing about sessions, events or actors; it is arithmetic over powers, so the rules
/// of fairness can be read, tested and changed in one place.
/// </summary>
/// <remarks>
/// Two steps. Admission: fleet requests first, then public ones, each class in arrival order;
/// a request is admitted if its minimum still fits. The first one that does not fit stops
/// admission for everyone behind it, so a smaller, later request never overtakes it. Then
/// water-filling: the capacity above the minimums is poured into the admitted requests, fleet
/// first; within a class every request rises at the same rate until it reaches its maximum or
/// the capacity runs out. Shares are rounded down to whole watts, so the allocations never add
/// up to more than the capacity.
/// </remarks>
public static class FairShareAllocator
{
    /// <summary>
    /// One allocation per admitted request, in the order of the requests. A request without an
    /// allocation is queued.
    /// </summary>
    public static ImmutableArray<PowerAllocation> Allocate(
        Power capacity, IReadOnlyList<AllocationRequest> requests)
    {
        var levels = new Power?[requests.Count];
        var unallocated = capacity;
        var classes = Enumerable.Range(0, requests.Count)
            .GroupBy(index => Rank(requests[index].Priority))
            .OrderBy(priorityClass => priorityClass.Key)
            .Select(priorityClass => priorityClass.ToArray())
            .ToArray();

        admission:
        foreach (var priorityClass in classes)
        {
            foreach (var index in priorityClass)
            {
                if (requests[index].MinPower > unallocated)
                {
                    break admission;
                }

                levels[index] = requests[index].MinPower;
                unallocated -= requests[index].MinPower;
            }
        }

        foreach (var priorityClass in classes)
        {
            unallocated = Fill(requests, levels, priorityClass, unallocated);
        }

        return [.. requests.Index()
            .Where(request => levels[request.Index] is not null)
            .Select(request => new PowerAllocation(
                request.Item.ConnectorId, levels[request.Index]!.Value))];
    }

    /// <summary>Raises one class's admitted requests evenly; returns what is left.</summary>
    private static Power Fill(
        IReadOnlyList<AllocationRequest> requests,
        Power?[] levels,
        int[] priorityClass,
        Power unallocated)
    {
        var rising = priorityClass.Where(index => levels[index] is not null).ToList();
        rising.RemoveAll(index => levels[index] >= requests[index].MaxPower);

        while (rising.Count > 0)
        {
            var share = Power.FromWatts(decimal.Floor(unallocated.Watts / rising.Count));
            var raise = rising
                .Select(index => requests[index].MaxPower - levels[index]!.Value)
                .Append(share)
                .MinBy(power => power.Kilowatts);
            if (raise == Power.Zero)
            {
                break;
            }

            foreach (var index in rising)
            {
                levels[index] += raise;
            }

            unallocated -= Power.FromWatts(raise.Watts * rising.Count);
            rising.RemoveAll(index => levels[index] >= requests[index].MaxPower);
        }

        return unallocated;
    }

    /// <summary>Fleet before public. A new priority class does not compile until ranked.</summary>
    private static int Rank(PriorityClass priority) => priority switch
    {
        Fleet => 0,
        Public => 1,
    };
}
