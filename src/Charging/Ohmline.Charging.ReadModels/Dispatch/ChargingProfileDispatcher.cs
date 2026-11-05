using System.Collections.Immutable;
using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Contracts.Chargers;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.Charging.ReadModels.Dispatch;

/// <summary>
/// Brings every change of a site's allocations to the chargers (section 16.8): a projection
/// with no table, on Site Energy's <c>site-energy</c> tag, that sends each connector its new
/// limit as a charging profile through the journey's charger port. A curtailment from Northgate
/// becomes a rebalance in Site Energy, and the rebalance becomes profiles here.
/// </summary>
/// <remarks>
/// At least once: a profile whose answer is lost is sent again with its event. Each profile
/// carries the allocation's version, the site budget's sequence number, so the gateway applies
/// it once and never sends a charger back to an older allocation. Site Energy is read through
/// its published view of its events (<see cref="ISiteAllocationEvent"/>), never its own types.
/// </remarks>
public sealed class ChargingProfileDispatcher(IChargerCommandPort? chargers = null)
    : IProjectionHandler
{
    /// <summary>Durable: its offsets are kept under it.</summary>
    public const string Id = "charging-profile-dispatcher-v1";

    public string ProjectionId => Id;

    public IReadOnlyList<string> Tags { get; } = [SiteEnergyTags.SiteEnergy];

    /// <summary>
    /// A charger that cannot be told keeps its old limit, which the next allocation corrects;
    /// a dispatcher that stops leaves every charger on its old limit. It goes on, and records
    /// what it skipped.
    /// </summary>
    public ProjectionFailurePolicy FailurePolicy { get; } = new RecordAndSkip(MaxRetries: 3);

    /// <summary>In a node, the profiles go through the charger port in its container.</summary>
    public IProjectionHandler RunningIn(ActorSystem system) =>
        new ChargingProfileDispatcher(
            DependencyResolver.For(system).Resolver.GetService<IChargerCommandPort>());

    public async Task<ImmutableArray<LiveUpdate>> HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        if (envelope.Event is not ISiteAllocationEvent allocations)
        {
            return [];
        }

        var port = chargers ?? throw new InvalidOperationException(
            $"{nameof(ChargingProfileDispatcher)} tells chargers only when it runs in a node.");
        await Task.WhenAll(ProfilesFor(allocations, envelope.SequenceNr).Select(profile =>
            port.SetChargingProfileAsync(
                profile.ConnectorId, profile.Limit, profile.Version, cancellationToken)));
        return [];
    }

    /// <summary>
    /// A profile for every connector the event gives a limit, zero for one that waits; none
    /// for a connector whose session released its power.
    /// </summary>
    public static ImmutableArray<SetChargingProfile> ProfilesFor(
        ISiteAllocationEvent allocations, long version) =>
        [
            .. allocations.Allocations
                .Where(allocation => allocation.Allocated is not null)
                .Select(allocation => new SetChargingProfile(
                    allocation.ConnectorId, allocation.Allocated!.Value, version)),
        ];
}
