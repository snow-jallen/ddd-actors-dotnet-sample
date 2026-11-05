using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;
using static Ohmline.SiteEnergy.Domain.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Domain.Tests.Budgets;

public sealed class FairShareAllocatorTests
{
    [Fact]
    public void Given_no_requests_then_there_are_no_allocations()
    {
        FairShareAllocator.Allocate(new Power(190m), []).ShouldBeEmpty();
    }

    [Fact]
    public void Given_five_cars_asking_for_50_kW_at_the_depot_then_each_gets_38_kW()
    {
        var allocations = FairShareAllocator.Allocate(new Power(190m), Requests(5));

        allocations.Select(allocation => allocation.Power).ShouldAllBe(power => power == Kw(38m));
        allocations.Select(allocation => allocation.ConnectorId).ShouldBe(Connectors[..5]);
    }

    [Fact]
    public void Given_a_curtailment_of_40_kW_then_each_of_the_five_drops_to_30_kW()
    {
        FairShareAllocator.Allocate(new Power(150m), Requests(5))
            .ShouldAllBe(allocation => allocation.Power == Kw(30m));
    }

    [Fact]
    public void Given_requests_that_all_fit_then_each_gets_its_maximum()
    {
        FairShareAllocator.Allocate(new Power(190m), Requests(3))
            .ShouldAllBe(allocation => allocation.Power == Kw(50m));
    }

    [Fact]
    public void Given_a_request_that_needs_little_then_what_it_cannot_take_goes_to_the_others()
    {
        AllocationRequest[] requests =
            [Public(0, max: 10m), Public(1, max: 50m), Public(2, max: 50m)];

        Powers(FairShareAllocator.Allocate(new Power(100m), requests)).ShouldBe([10m, 45m, 45m]);
    }

    [Fact]
    public void Given_fleet_and_public_requests_then_the_fleet_is_filled_first()
    {
        AllocationRequest[] requests = [Public(0), Fleet(1), Public(2), Fleet(3)];

        Powers(FairShareAllocator.Allocate(new Power(100m), requests)).ShouldBe([5m, 45m, 5m, 45m]);
    }

    [Fact]
    public void Given_room_for_only_some_minimums_then_the_later_requests_are_queued()
    {
        var allocations = FairShareAllocator.Allocate(new Power(12m), Requests(3));

        allocations.ShouldBe([new(Connectors[0], Kw(6m)), new(Connectors[1], Kw(6m))]);
    }

    [Fact]
    public void Given_a_waiting_request_then_a_later_smaller_one_does_not_overtake_it()
    {
        AllocationRequest[] requests =
            [Public(0), Public(1, min: 10m), Public(2, min: 2m)];

        FairShareAllocator.Allocate(new Power(12m), requests)
            .ShouldBe([new PowerAllocation(Connectors[0], Kw(12m))]);
    }

    [Fact]
    public void Given_a_fleet_request_that_does_not_fit_then_no_public_request_overtakes_it()
    {
        AllocationRequest[] requests = [Public(0, min: 1m), Fleet(1, min: 8m), Fleet(2, min: 8m)];

        FairShareAllocator.Allocate(new Power(10m), requests)
            .ShouldBe([new PowerAllocation(Connectors[1], Kw(10m))]);
    }

    [Fact]
    public void Given_no_capacity_then_every_request_is_queued()
    {
        FairShareAllocator.Allocate(Power.Zero, Requests(3)).ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_share_that_does_not_divide_evenly_then_whole_watts_never_exceed_capacity()
    {
        AllocationRequest[] requests =
            [Public(0, max: 100m), Public(1, max: 100m), Public(2, max: 100m)];

        var allocations = FairShareAllocator.Allocate(new Power(200m), requests);

        Powers(allocations).ShouldBe([66.666m, 66.666m, 66.666m]);
        Sum(allocations).ShouldBe(Kw(199.998m));
    }

    [Fact]
    public void Given_three_hundred_buses_at_shift_change_then_the_invariant_holds_for_every_one()
    {
        AllocationRequest[] requests =
        [
            .. Enumerable.Range(0, 300).Select(bus => new AllocationRequest(
                new ConnectorId(new ChargePointId($"OHM-DEP-{bus / 2:D4}"), bus % 2 + 1),
                Kw(5m),
                bus % 4 == 0 ? Kw(150m) : Kw(50m),
                bus % 3 == 0 ? new Fleet() : new Public())),
        ];
        var capacity = new Power(2000m);

        var allocations = FairShareAllocator.Allocate(capacity, requests);

        allocations.Length.ShouldBe(300);
        (Sum(allocations) <= capacity).ShouldBeTrue();
        foreach (var (request, allocation) in requests.Zip(allocations))
        {
            allocation.ConnectorId.ShouldBe(request.ConnectorId);
            (allocation.Power >= request.MinPower && allocation.Power <= request.MaxPower)
                .ShouldBeTrue();
            allocation.Power.ShouldBe(request.Priority is Fleet ? Kw(10m) : Kw(5m));
        }
    }

    [Fact]
    public void Given_an_allocation_then_the_decision_says_whether_it_was_granted_in_full()
    {
        var request = Public(0);
        PowerAllocation[] full = [new(Connectors[0], Kw(50m))];
        PowerAllocation[] partial = [new(Connectors[0], Kw(38m))];

        AllocationDecision.Of(request, [.. full]).Value
            .ShouldBe(new Granted(new(Connectors[0], Kw(50m))));
        AllocationDecision.Of(request, [.. partial]).Value
            .ShouldBe(new Reduced(new(Connectors[0], Kw(38m)), Kw(50m)));
        AllocationDecision.Of(request, []).Value.ShouldBe(new Queued(Connectors[0]));
    }

    private static AllocationRequest[] Requests(int count) =>
        [.. Enumerable.Range(0, count).Select(index => Public(index))];

    private static AllocationRequest Public(int index, decimal min = 5m, decimal max = 50m) =>
        new(Connectors[index], Kw(min), Kw(max), new Public());

    private static AllocationRequest Fleet(int index, decimal min = 5m, decimal max = 50m) =>
        new(Connectors[index], Kw(min), Kw(max), new Fleet());

    private static decimal[] Powers(IEnumerable<PowerAllocation> allocations) =>
        [.. allocations.Select(allocation => allocation.Power.Kilowatts)];

    private static Power Sum(IEnumerable<PowerAllocation> allocations) =>
        allocations.Aggregate(Power.Zero, (sum, allocation) => sum + allocation.Power);

    private static Power Kw(decimal kilowatts) => new(kilowatts);
}
