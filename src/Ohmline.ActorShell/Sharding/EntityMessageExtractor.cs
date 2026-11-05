using Akka.Cluster.Sharding;

namespace Ohmline.ActorShell.Sharding;

/// <summary>
/// Tells a shard region which entity a message is for: the id the message itself carries, read
/// by a function that each bounded context supplies as a switch over its own message families
/// (a charge point command carries its charge point's id). The shard is a hash of that id, so
/// the same entity always lands in the same shard, and every node computes the same answer.
/// </summary>
/// <remarks>
/// Sharding's own messages, such as <c>ShardRegion.StartEntity</c>, are handled by Akka.NET
/// before the extractor sees them; handling them here is a mistake the AK2001 analyzer reports.
/// A message the function does not know gets a null id, and the region drops it with a warning.
/// </remarks>
public sealed class EntityMessageExtractor(Func<object, string?> entityIdOf)
    : HashCodeMessageExtractor(NumberOfShards)
{
    /// <summary>
    /// Durable: entities are spread over this many shards, and shards over the nodes. About ten
    /// times the most nodes Ohmline will ever run. It never changes while any node of the cluster
    /// runs, because the same id would then hash to another shard, and two nodes could each start
    /// an actor for the same aggregate.
    /// </summary>
    public const int NumberOfShards = 100;

    public override string? EntityId(object message) => entityIdOf(message);
}
