using Akka.Actor;
using Akka.Serialization;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.DriverAccounts.Actors.Serialization;
using Ohmline.SiteEnergy.Actors.Serialization;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// Charging's serializer, registered for a test as AddChargingContext does; and the serializers
/// of the contexts whose commands and replies the charging journey exchanges.
/// </summary>
internal static class Serializers
{
    public static SerializerDetails Charging(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            ChargingSerializer.Name,
            new ChargingSerializer(system),
            [.. ChargingSerializer.BoundTypes]);

    public static SerializerDetails DriverAccounts(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            DriverAccountsSerializer.Name,
            new DriverAccountsSerializer(system),
            [.. DriverAccountsSerializer.BoundTypes]);

    public static SerializerDetails SiteEnergy(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            SiteEnergySerializer.Name,
            new SiteEnergySerializer(system),
            [.. SiteEnergySerializer.BoundTypes]);
}
