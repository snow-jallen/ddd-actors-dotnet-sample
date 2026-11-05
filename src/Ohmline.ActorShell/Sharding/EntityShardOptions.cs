using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Ohmline.ActorShell.Clustering;

namespace Ohmline.ActorShell.Sharding;

/// <summary>The shard options every region of aggregates in Ohmline starts from.</summary>
public static class EntityShardOptions
{
    /// <summary>
    /// A new set each time, because <see cref="ShardOptions"/> is mutable: regions live on the
    /// <c>charging</c> nodes, and the coordinator keeps where the shards are in distributed data.
    /// </summary>
    public static ShardOptions Create() => new()
    {
        Role = ClusterRoles.Charging,
        // Not Akka.NET 1.5's default, persistence, which is deprecated. Changing it later
        // needs a full cluster restart, so it is chosen before the first deployment.
        StateStoreMode = StateStoreMode.DData,
    };
}
