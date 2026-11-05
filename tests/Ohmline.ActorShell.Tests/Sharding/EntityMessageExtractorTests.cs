using Ohmline.ActorShell.Sharding;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.ActorShell.Tests.Sharding;

public sealed class EntityMessageExtractorTests
{
    private sealed record Knock(string Door);

    private static readonly EntityMessageExtractor Doors = new(message => message switch
    {
        Knock knock => knock.Door,
        _ => null,
    });

    [Fact]
    public void Given_a_message_its_context_knows_then_the_entity_is_the_id_it_carries() =>
        Doors.EntityId(new Knock("OHM-SLC-0042")).ShouldBe("OHM-SLC-0042");

    [Fact]
    public void Given_a_message_its_context_does_not_know_then_it_has_no_entity() =>
        Doors.EntityId("a stranger").ShouldBeNull();

    [Fact]
    public void Given_a_message_then_the_entity_receives_it_unchanged()
    {
        var knock = new Knock("OHM-SLC-0042");

        Doors.EntityMessage(knock).ShouldBeSameAs(knock);
    }

    [Fact]
    public void Given_a_traced_message_then_it_is_routed_by_the_message_inside()
    {
        var traced = new TraceEnvelope(new Knock("OHM-SLC-0042"), "00-x-y-01", null);

        Doors.EntityId(traced).ShouldBe("OHM-SLC-0042");
    }

    [Fact]
    public void Given_a_traced_message_then_the_entity_receives_the_whole_envelope()
    {
        var traced = new TraceEnvelope(new Knock("OHM-SLC-0042"), "00-x-y-01", null);

        Doors.EntityMessage(traced).ShouldBeSameAs(traced);
    }

    [Fact]
    public void Given_any_entity_id_then_its_shard_is_one_of_100()
    {
        var shards = Enumerable.Range(0, 10_000)
            .Select(n => int.Parse(Doors.ShardId($"OHM-SLC-{n:0000}", messageHint: null)!))
            .ToHashSet();

        shards.ShouldAllBe(shard => shard >= 0 && shard < 100);
        shards.Count.ShouldBe(100); // Ten thousand charge points use every shard.
    }

    /// <summary>
    /// Durable: the shard of an id is a hash that every node, every process and every version
    /// must compute alike (it is not <c>string.GetHashCode</c>, which differs per process).
    /// These values are what the cluster has always computed; if this test fails, entities would
    /// move to other shards in a rolling update.
    /// </summary>
    [Theory]
    [InlineData("OHM-SLC-0042", "67")]
    [InlineData("OHM-SLC-0043", "2")]
    [InlineData("slc-depot", "20")]
    public void Given_an_entity_id_then_its_shard_never_changes(string entityId, string shard) =>
        Doors.ShardId(entityId, messageHint: null).ShouldBe(shard);
}
