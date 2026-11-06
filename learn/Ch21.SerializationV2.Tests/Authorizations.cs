using Akka.Actor;
using Akka.Serialization;
using Akka.Serialization.V2;

namespace Ch21.SerializationV2.Tests;

/// <summary>The messages one serializer handles: everything that implements this.</summary>
public interface IAuthorizationMessage;

/// <summary>A driver who tapped an RFID card.</summary>
[AkkaSerializable(Manifest = "learn.rfid-card.v1")]
public sealed record RfidCard([property: AkkaField(0)] string TokenUid);

/// <summary>A driver who started from the app.</summary>
[AkkaSerializable(Manifest = "learn.app-token.v1")]
public sealed record AppToken(
    [property: AkkaField(0)] string DriverRef,
    [property: AkkaField(1)] string DeviceId);

/// <summary>How a session was authorized: one of the two, and nothing else.</summary>
public union Authorization(RfidCard, AppToken);

/// <summary>
/// A message with a union field. Akka.Serialization.V2 writes the case that the union holds,
/// with its manifest, and reads it back into the union: the field's value survives, where 1.5's
/// default serializer silently restores <c>Value == null</c> (section 3.9).
/// </summary>
[AkkaSerializable(Manifest = "learn.charging-authorized.v1")]
public sealed record ChargingAuthorized(
    [property: AkkaField(0)] string SessionId,
    [property: AkkaField(1)] Authorization By) : IAuthorizationMessage;

/// <summary>
/// The generated serializer for every <see cref="IAuthorizationMessage"/>. Its id is the last
/// of Ohmline's reserved range (9105 to 9199): a demonstration, never bound in a node.
/// </summary>
[AkkaSerializer<IAuthorizationMessage>("learn-authorizations", 9199)]
public sealed partial class AuthorizationSerializer : AkkaSerializer
{
    public static partial SerializerDetails CreateSerializerDetails(ExtendedActorSystem system);
}
