using Akka.Actor;
using Akka.Serialization;
using Ohmline.Charging.Actors.Serialization;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>Charging's serializer, registered for a test as AddChargingContext does.</summary>
internal static class Serializers
{
    public static SerializerDetails Charging(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            ChargingSerializer.Name,
            new ChargingSerializer(system),
            [.. ChargingSerializer.BoundTypes]);
}
