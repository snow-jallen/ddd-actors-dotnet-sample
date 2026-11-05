using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Ohmline.ActorShell.Sharding;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Actors.Budgets;
using Ohmline.SiteEnergy.Actors.Persistence;
using Ohmline.SiteEnergy.Actors.Serialization;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Actors;

/// <summary>Everything Site Energy adds to an ActorSystem, in one call.</summary>
public static class SiteEnergyAkkaExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Site Energy's serializer, bound to every message family it owns, and the site power
        /// budget shard region, registered as <see cref="SitePowerBudgetRegion"/>. The container
        /// makes each budget's actor, with the site id and the host's clock.
        /// </summary>
        public AkkaConfigurationBuilder AddSiteEnergyContext() =>
            akka
                .WithCustomSerializer(
                    SiteEnergySerializer.Name,
                    SiteEnergySerializer.BoundTypes,
                    system => new SiteEnergySerializer(system))
                .WithShardRegion<SitePowerBudgetRegion>(
                    SitePowerBudgetActor.TypeName,
                    (_, _, resolver) => entityId =>
                        resolver.Props<SitePowerBudgetActor>(new SiteId(entityId)),
                    SiteEnergyMessageExtractors.Budgets,
                    EntityShardOptions.Create());
    }

    extension(AkkaPersistenceJournalBuilder journal)
    {
        /// <summary>
        /// Site Energy's tagger, for the journal the host keeps its events in: every event is
        /// stored with the <c>site-energy</c> tag (section 13.2).
        /// </summary>
        public AkkaPersistenceJournalBuilder AddSiteEnergyTagger() =>
            journal.AddWriteEventAdapter<SiteEnergyTagger>(
                SiteEnergyTagger.Name, SiteEnergyTagger.BoundTypes);
    }
}
