using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Actors.Budgets;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;
using static Ohmline.SiteEnergy.Actors.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Actors.Tests.Serialization;

/// <summary>One message of every type Site Energy's serializer writes, all fields filled.</summary>
internal static class SiteEnergySamples
{
    public static readonly SiteConfiguration Depot = SiteConfiguration.Create(
        Site,
        new Power(200m),
        new Power(10m),
        "America/Denver",
        [
            ConnectorConfiguration.Create(Fleet1, new Power(5m), new Power(50m), new Fleet()),
            ConnectorConfiguration.Create(Public2, new Power(5m), new Power(50m), new Public()),
        ]);

    public static IReadOnlyList<object> All { get; } =
    [
        Configure(),
        Request(Fleet1, BusSession),
        new ReleaseAllocation(Command, Site, Fleet1, BusSession),
        new ApplyCurtailment(Command, Site, new Power(120m)),
        new EndCurtailment(Command, Site),

        Stamped(new SiteConfigured(Site, Depot)),
        Stamped(new AllocationGranted(Site, Fleet1, BusSession, new Power(50m), new Power(50m))),
        Stamped(new AllocationQueued(Site, Public2, CarSession, new Power(50m))),
        Stamped(new AllocationsRebalanced(
            Site,
            [new PowerAllocation(Fleet1, new Power(50m)), new PowerAllocation(Public2, new(20m))])),
        Stamped(new AllocationReleased(Site, Fleet1, BusSession)),
        Stamped(new CurtailmentApplied(Site, new Power(120m))),
        Stamped(new CurtailmentEnded(Site)),

        new BudgetCommandAccepted(Version: 4),
        new BudgetCommandRejected(
            "connector-has-open-request",
            $"Connector {Fleet1} already has a request, for session {BusSession}."),

        new SitePowerBudgetSnapshot(
            Depot,
            Curtailment: new Power(120m),
            [
                new OpenRequest(Fleet1, BusSession, new Power(50m)),
                new OpenRequest(Public2, CarSession, new Power(50m)),
            ],
            [new PowerAllocation(Fleet1, new Power(50m)), new PowerAllocation(Public2, new(20m))]),

        new GetSitePowerBudget(Site),
        new SitePowerBudgetView(
            Site,
            Version: 5,
            EffectiveCapacity: new Power(70m),
            Curtailment: new Power(120m),
            [
                new RequestView(Fleet1, BusSession, new Power(50m), new Power(50m)),
                new RequestView(Public2, CarSession, new Power(50m), Allocated: null),
            ]),
    ];

    private static SitePowerBudgetEvent Stamped(SitePowerBudgetEvent @event) =>
        @event with { CommandId = Command, OccurredAt = Morning };
}
