using Akka.Hosting;
using Ohmline.SiteEnergy.Actors.Serialization;

namespace Ohmline.SiteEnergy.Actors;

/// <summary>Everything Site Energy adds to an ActorSystem, in one call.</summary>
public static class SiteEnergyAkkaExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Site Energy's serializer, bound to every message family it owns. The site power
        /// budget actors arrive with the shard region in section 12.2.
        /// </summary>
        public AkkaConfigurationBuilder AddSiteEnergyContext() =>
            akka.WithCustomSerializer(
                SiteEnergySerializer.Name,
                SiteEnergySerializer.BoundTypes,
                system => new SiteEnergySerializer(system));
    }
}
