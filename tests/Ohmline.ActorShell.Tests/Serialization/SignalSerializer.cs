using Akka.Actor;
using Ohmline.ActorShell.Serialization;
using Ohmline.ActorShell.Tests.Messaging;

namespace Ohmline.ActorShell.Tests.Serialization;

/// <summary>A serializer for the test protocol; never registered, so its id is free.</summary>
public sealed class SignalSerializer(
    ExtendedActorSystem system,
    IReadOnlyDictionary<Type, string> manifests,
    IReadOnlyDictionary<string, Upcaster>? upcasters = null)
    : ManifestJsonSerializer(system, manifests, upcasters ?? Upcasters)
{
    /// <summary>The first <see cref="Pong"/>, whose count was written as text.</summary>
    public static IReadOnlyDictionary<string, Upcaster> Upcasters { get; } =
        new Dictionary<string, Upcaster>
        {
            ["test.pong.v0"] = Upcaster.Create<PongV0, Pong>(v0 => new Pong(int.Parse(v0.Count))),
        };

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
