using Akka.Cluster.Hosting;
using Akka.Hosting;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors;

/// <summary>
/// Everything Charging adds to an ActorSystem, in one call. The node composes bounded contexts
/// with one such extension each, so a context's wiring lives next to its actors, and a test can
/// start exactly the same wiring.
/// </summary>
public static class ChargingAkkaExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Charging's serializer, bound to every message family Charging owns, and a shard
        /// region per aggregate: the repository of charge points, sessions and listings. Each
        /// region is registered under its key from the contracts, such as
        /// <see cref="ChargePointRegion"/>. The journal and the cluster are the host's choice.
        /// </summary>
        public AkkaConfigurationBuilder AddChargingContext() =>
            akka
                .WithCustomSerializer(
                    ChargingSerializer.Name,
                    ChargingSerializer.BoundTypes,
                    system => new ChargingSerializer(system))
                .WithShardRegion<ChargePointRegion>(
                    ChargePointActor.TypeName,
                    (_, _, resolver) =>
                    {
                        var clock = resolver.GetService<TimeProvider>();
                        return entityId =>
                            ChargePointActor.Props(new ChargePointId(entityId), clock);
                    },
                    ChargingMessageExtractors.ChargePoints,
                    EntityShardOptions.Create())
                .WithShardRegion<ChargingSessionRegion>(
                    ChargingSessionActor.TypeName,
                    (_, _, resolver) =>
                    {
                        var clock = resolver.GetService<TimeProvider>();
                        return entityId =>
                            ChargingSessionActor.Props(new SessionId(Guid.Parse(entityId)), clock);
                    },
                    ChargingMessageExtractors.Sessions,
                    EntityShardOptions.Create())
                .WithShardRegion<ChargePointListingRegion>(
                    ChargePointListingActor.TypeName,
                    (_, _, resolver) =>
                    {
                        var clock = resolver.GetService<TimeProvider>();
                        return entityId =>
                            ChargePointListingActor.Props(new ChargePointId(entityId), clock);
                    },
                    ChargingMessageExtractors.Listings,
                    EntityShardOptions.Create());
    }
}
