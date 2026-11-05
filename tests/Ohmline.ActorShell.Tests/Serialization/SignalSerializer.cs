using Akka.Actor;
using Ohmline.ActorShell.Serialization;
using Ohmline.ActorShell.Tests.Messaging;

namespace Ohmline.ActorShell.Tests.Serialization;

/// <summary>A serializer for the test protocol; never registered, so its id is free.</summary>
public sealed class SignalSerializer(
    ExtendedActorSystem system, IReadOnlyDictionary<Type, string> manifests)
    : ManifestJsonSerializer(system, manifests)
{
    public static IReadOnlyDictionary<Type, string> Manifests { get; } =
        new Dictionary<Type, string>
        {
            [typeof(Ping)] = "test.ping.v1",
            [typeof(Pong)] = "test.pong.v1",
            [typeof(Chime)] = "test.chime.v1",
            [typeof(Gong)] = "test.gong.v1",
        };

    public override int Identifier => 9199;
}
