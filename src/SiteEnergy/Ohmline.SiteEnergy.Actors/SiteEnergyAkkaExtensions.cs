using Akka.Cluster.Hosting;
using Akka.Hosting;
using Ohmline.ActorShell.Sharding;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Actors.Budgets;
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
        /// budget shard region, registered as <see cref="SitePowerBudgetRegion"/>.
        /// </summary>
        public AkkaConfigurationBuilder AddSiteEnergyContext() =>
            akka
                .WithCustomSerializer(
                    SiteEnergySerializer.Name,
                    SiteEnergySerializer.BoundTypes,
                    system => new SiteEnergySerializer(system))
                .WithShardRegion<SitePowerBudgetRegion>(
                    SitePowerBudgetActor.TypeName,
                    (_, _, resolver) =>
                    {
                        var clock = resolver.GetService<TimeProvider>();
                        return entityId => SitePowerBudgetActor.Props(new SiteId(entityId), clock);
                    },
                    SiteEnergyMessageExtractors.Budgets,
                    EntityShardOptions.Create());
    }
}
