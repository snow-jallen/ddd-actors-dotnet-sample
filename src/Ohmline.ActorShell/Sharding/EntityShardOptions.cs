using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Ohmline.ActorShell.Clustering;

namespace Ohmline.ActorShell.Sharding;

/// <summary>The shard options every region of aggregates in Ohmline starts from.</summary>
public static class EntityShardOptions
{
    /// <summary>
    /// A new set each time, because <see cref="ShardOptions"/> is mutable: regions live on the
    /// <c>charging</c> nodes, the coordinator keeps where the shards are in distributed data, and
    /// an entity idle for two minutes is stopped with <see cref="Stop"/> and started again,
    /// from its journal, by the next message for it.
    /// </summary>
    public static ShardOptions Create() => new()
    {
        Role = ClusterRoles.Charging,
        // Not Akka.NET 1.5's default, persistence, which is deprecated. Changing it later
        // needs a full cluster restart, so it is chosen before the first deployment.
        StateStoreMode = StateStoreMode.DData,
        HandOffStopMessage = Stop.Instance,
        PassivateIdleEntityAfter = TimeSpan.FromMinutes(2),
        // Remembering entities would restart every one of them after a rebalance or a restart,
        // and it switches idle passivation off. An aggregate needs neither: it waits in the
        // journal until a command arrives. The journey, which must run without one, opts in.
        RememberEntities = false,
    };
}
