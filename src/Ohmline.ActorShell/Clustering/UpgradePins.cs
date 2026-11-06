using Akka.Hosting;

namespace Ohmline.ActorShell.Clustering;

/// <summary>
/// What the upgrade from Akka.NET 1.5 to 1.6 must not change on its own (section 21.10), written
/// down so that a node of either version behaves the same during the rolling upgrade, and a
/// rollback to 1.5 reads whatever 1.6 wrote.
/// </summary>
/// <remarks>
/// <para>
/// <b>Serializer bindings.</b> 1.6 registers Akka.NET's own serializers in code (#8702), where
/// 1.5 bound them in the HOCON of Akka.Remote, which every Ohmline node loads. The bindings are
/// the same today, but they now depend on code a later 1.6 release may change, and a 1.5
/// process without Akka.Remote binds a <c>string</c> to JSON. These bindings pin the 1.5
/// behaviour for every type that can reach the journal or the wire outside Ohmline's own
/// serializers: primitives to <c>primitive</c> (17), actor references and the status replies
/// to <c>akka-misc</c> (16).
/// </para>
/// <para>
/// <b>Rebalancing.</b> 1.6 changes the default of <c>rebalance-absolute-limit</c> from 0 (1.5's
/// algorithm: at most three shards at a time, once a node has one more than another) to 20
/// (the bounded algorithm, #8445). The coordinator runs on the oldest node, so during a rolling
/// upgrade the algorithm would change at an unpredictable moment. It stays at 0 until every
/// node runs 1.6; a later release may choose 20 on purpose.
/// </para>
/// </remarks>
public static class UpgradePins
{
    public const string Hocon = """
        akka.actor {
          serializers {
            primitive = "Akka.Remote.Serialization.PrimitiveSerializers, Akka.Remote"
            akka-misc = "Akka.Remote.Serialization.MiscMessageSerializer, Akka.Remote"
          }
          serialization-bindings {
            "System.String" = primitive
            "System.Int32" = primitive
            "System.Int64" = primitive
            "Akka.Actor.IActorRef, Akka" = akka-misc
            "Akka.Actor.Status+Success, Akka" = akka-misc
            "Akka.Actor.Status+Failure, Akka" = akka-misc
            "Akka.Actor.Identify, Akka" = akka-misc
            "Akka.Actor.ActorIdentity, Akka" = akka-misc
          }
        }
        akka.cluster.sharding.least-shard-allocation-strategy {
          rebalance-absolute-limit = 0
          rebalance-relative-limit = 0.1
        }
        """;

    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>The 1.5 behaviour, ahead of Akka.NET's defaults.</summary>
        public AkkaConfigurationBuilder WithUpgradePins() =>
            akka.AddHocon(Hocon, HoconAddMode.Prepend);
    }
}
