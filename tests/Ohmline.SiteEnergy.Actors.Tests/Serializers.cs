using Akka.Actor;
using Akka.Serialization;
using Ohmline.SiteEnergy.Actors.Serialization;

namespace Ohmline.SiteEnergy.Actors.Tests;

/// <summary>Site Energy's serializer, registered for a test as AddSiteEnergyContext does.</summary>
internal static class Serializers
{
    public static SerializerDetails SiteEnergy(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            SiteEnergySerializer.Name,
            new SiteEnergySerializer(system),
            [.. SiteEnergySerializer.BoundTypes]);
}
