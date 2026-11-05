using System.Collections.Frozen;
using System.Text.Json;
using Akka.Actor;
using Ohmline.ActorShell.Serialization;
using Ohmline.SiteEnergy.Actors.Budgets;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Serialization;

/// <summary>
/// Everything Site Energy tells, replies or persists, as JSON under a permanent manifest. The
/// identifier and the manifests are never changed or reused (ADR 0007).
/// </summary>
public sealed class SiteEnergySerializer(ExtendedActorSystem system)
    : ManifestJsonSerializer(system, Manifests, Configure)
{
    /// <summary>The name Akka.NET knows the serializer by, in configuration and logs.</summary>
    public const string Name = "site-energy";

    /// <summary>Every message whose type derives from one of these is written here.</summary>
    public static IReadOnlyList<Type> BoundTypes { get; } =
    [
        typeof(SitePowerBudgetCommand),
        typeof(SitePowerBudgetEvent),
        typeof(BudgetCommandReply),
        typeof(SitePowerBudgetSnapshot),
    ];

    public static FrozenDictionary<Type, string> Manifests { get; } = new Dictionary<Type, string>
    {
        [typeof(ConfigureSite)] = "site-energy.configure-site.v1",
        [typeof(RequestAllocation)] = "site-energy.request-allocation.v1",
        [typeof(ReleaseAllocation)] = "site-energy.release-allocation.v1",
        [typeof(ApplyCurtailment)] = "site-energy.apply-curtailment.v1",
        [typeof(EndCurtailment)] = "site-energy.end-curtailment.v1",

        [typeof(SiteConfigured)] = "site-energy.site-configured.v1",
        [typeof(AllocationGranted)] = "site-energy.allocation-granted.v1",
        [typeof(AllocationQueued)] = "site-energy.allocation-queued.v1",
        [typeof(AllocationsRebalanced)] = "site-energy.allocations-rebalanced.v1",
        [typeof(AllocationReleased)] = "site-energy.allocation-released.v1",
        [typeof(CurtailmentApplied)] = "site-energy.curtailment-applied.v1",
        [typeof(CurtailmentEnded)] = "site-energy.curtailment-ended.v1",

        [typeof(BudgetCommandAccepted)] = "site-energy.budget-command-accepted.v1",
        [typeof(BudgetCommandRejected)] = "site-energy.budget-command-rejected.v1",

        [typeof(SitePowerBudgetSnapshot)] = "site-energy.site-power-budget-snapshot.v1",
    }.ToFrozenDictionary();

    /// <summary>Permanent: Ohmline's serializer ids are listed in ADR 0007.</summary>
    public override int Identifier => 9102;

    private static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new SiteConfigurationConverter());
        options.Converters.Add(new ConnectorConfigurationConverter());
        options.AddClosedHierarchy<PriorityClass>(
            (typeof(Fleet), "fleet"),
            (typeof(Public), "public"));
    }
}
