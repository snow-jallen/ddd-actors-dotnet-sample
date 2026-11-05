using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Actors.Persistence;
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
        /// <remarks>
        /// An entity's actor is made by the container: the region passes the id it routed
        /// on, and everything else the constructor asks for, the clock first, comes from the
        /// host's services. A test that registers a fake clock gets entities that live by it.
        /// </remarks>
        public AkkaConfigurationBuilder AddChargingContext() =>
            akka
                .WithCustomSerializer(
                    ChargingSerializer.Name,
                    ChargingSerializer.BoundTypes,
                    system => new ChargingSerializer(system))
                .WithShardRegion<ChargePointRegion>(
                    ChargePointActor.TypeName,
                    (_, _, resolver) => entityId =>
                        resolver.Props<ChargePointActor>(new ChargePointId(entityId)),
                    ChargingMessageExtractors.ChargePoints,
                    EntityShardOptions.Create())
                .WithShardRegion<ChargingSessionRegion>(
                    ChargingSessionActor.TypeName,
                    (_, _, resolver) => entityId =>
                        resolver.Props<ChargingSessionActor>(new SessionId(Guid.Parse(entityId))),
                    ChargingMessageExtractors.Sessions,
                    EntityShardOptions.Create())
                .WithShardRegion<ChargePointListingRegion>(
                    ChargePointListingActor.TypeName,
                    (_, _, resolver) => entityId =>
                        resolver.Props<ChargePointListingActor>(new ChargePointId(entityId)),
                    ChargingMessageExtractors.Listings,
                    EntityShardOptions.Create());
    }

    extension(AkkaPersistenceJournalBuilder journal)
    {
        /// <summary>
        /// Charging's tagger, for the journal the host keeps Charging's events in: every event
        /// Charging persists is stored with its tag (section 13.2). The host calls it where it
        /// configures that journal.
        /// </summary>
        public AkkaPersistenceJournalBuilder AddChargingTagger() =>
            journal.AddWriteEventAdapter<ChargingTagger>(
                ChargingTagger.Name, ChargingTagger.BoundTypes);
    }
}
