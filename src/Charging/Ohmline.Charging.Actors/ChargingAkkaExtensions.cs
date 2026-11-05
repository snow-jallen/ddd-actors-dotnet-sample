using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Journeys;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Actors.Persistence;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Journeys;
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
        /// region per aggregate: the repository of charge points, sessions and listings; and
        /// one for the charging journeys. Each region is registered under its key from the
        /// contracts, such as <see cref="ChargePointRegion"/>. The journal and the cluster are
        /// the host's choice.
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
                    EntityShardOptions.Create())
                .WithShardRegion<ChargingJourneyRegion>(
                    ChargingJourneyActor.TypeName,
                    (_, _, resolver) => entityId =>
                        resolver.Props<ChargingJourneyActor>(new JourneyId(Guid.Parse(entityId))),
                    ChargingMessageExtractors.Journeys,
                    JourneyShardOptions())
                // Sharding's distributed data is one per role, shared by every region: the
                // journeys' remembered entities are its only durable keys, kept in memory.
                .WithShardingDistributedData(ddata => ddata.Durable.Keys = []);
    }

    /// <summary>
    /// The journeys' region remembers its entities (section 15.3): a journey that waits for a
    /// reply or a timer is started again after a rebalance or the loss of its node, and
    /// recovers, rather than wait for a message that is not coming. That switches idle
    /// passivation off, so a journey passivates itself once it is over.
    /// </summary>
    /// <remarks>
    /// The list of journeys is kept in distributed data, in memory, on every charging node: it
    /// survives a rebalance and the loss of a node, but not the loss of every node at once.
    /// Akka.NET can keep it in files on each node's disk instead (the default, which needs a
    /// volume per pod), or in the journal (<see cref="RememberEntitiesStore.Eventsourced"/>),
    /// where the sharding's own events would take places in the journal's global order beside
    /// Ohmline's events, and its persistence ids would show up among the aggregates'.
    /// </remarks>
    private static ShardOptions JourneyShardOptions()
    {
        var options = EntityShardOptions.Create();
        options.RememberEntities = true;
        options.RememberEntitiesStore = RememberEntitiesStore.DData;
        return options;
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
