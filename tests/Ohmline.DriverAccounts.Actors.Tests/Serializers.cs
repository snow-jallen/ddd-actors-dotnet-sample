using Akka.Actor;
using Akka.Serialization;
using Ohmline.DriverAccounts.Actors.Serialization;

namespace Ohmline.DriverAccounts.Actors.Tests;

/// <summary>Driver Accounts' serializer, registered for a test as its context does.</summary>
internal static class Serializers
{
    public static SerializerDetails DriverAccounts(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            DriverAccountsSerializer.Name,
            new DriverAccountsSerializer(system),
            [.. DriverAccountsSerializer.BoundTypes]);
}
