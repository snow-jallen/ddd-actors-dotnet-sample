using Akka.Actor;
using Akka.Serialization;
using Ohmline.Tariffs.Actors.Serialization;

namespace Ohmline.Tariffs.Actors.Tests;

/// <summary>Tariffs' serializer, registered for a test as AddTariffsContext does.</summary>
internal static class Serializers
{
    public static SerializerDetails Tariffs(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            TariffsSerializer.Name,
            new TariffsSerializer(system),
            [.. TariffsSerializer.BoundTypes]);
}
