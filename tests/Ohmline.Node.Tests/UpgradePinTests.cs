using Akka.Actor;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Node.Tests;

/// <summary>
/// What a node writes to the journal and the wire, and how it rebalances, does not change with
/// the Akka.NET version (section 21.10): the same serializer for every type as on 1.5.73, and
/// 1.5's rebalance algorithm. The same tests pass on 1.5.73 and on 1.6.0-beta5.
/// </summary>
public sealed class UpgradePinTests(ITestOutputHelper output) : ShardedNode(output)
{
    public static TheoryData<string, int> Bindings => new()
    {
        { "string", 17 },
        { "int", 17 },
        { "long", 17 },
        { "actor reference", 16 },
        { "status success", 16 },
        { "status failure", 16 },
        { "trace envelope", 9100 },
        { "charging command", 9101 },
    };

    [Theory]
    [MemberData(nameof(Bindings))]
    public void Given_a_node_then_each_type_has_the_serializer_it_had_on_1_5(
        string kind, int serializerId)
    {
        object message = kind switch
        {
            "string" => "OHM-SLC-0042",
            "int" => 42,
            "long" => 42L,
            "actor reference" => TestActor,
            "status success" => new Status.Success("ok"),
            "status failure" => new Status.Failure(new InvalidOperationException("no")),
            "trace envelope" => new TraceEnvelope(
                new StopSession(
                    new CommandId(Guid.CreateVersion7()), new SessionId(Guid.CreateVersion7())),
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                null),
            "charging command" => new StopSession(
                new CommandId(Guid.CreateVersion7()), new SessionId(Guid.CreateVersion7())),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        Sys.Serialization.FindSerializerFor(message).Identifier.ShouldBe(serializerId);
    }

    [Fact]
    public void Given_a_node_then_shards_rebalance_with_1_5_s_algorithm()
    {
        var allocation = Sys.Settings.Config.GetConfig(
            "akka.cluster.sharding.least-shard-allocation-strategy");

        allocation.GetInt("rebalance-absolute-limit").ShouldBe(0);
        allocation.GetDouble("rebalance-relative-limit").ShouldBe(0.1);
    }
}
