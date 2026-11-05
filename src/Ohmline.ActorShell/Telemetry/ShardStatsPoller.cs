using Akka.Actor;
using Akka.Cluster.Sharding;

namespace Ohmline.ActorShell.Telemetry;

/// <summary>
/// Asks every shard region on this node, on a timer, for its statistics across the cluster
/// (section 18.2), and leaves the totals per type name for the shell's gauges: how many shards,
/// and how many entities are alive in them.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetClusterShardingStats"/> asks every region in the cluster, so a poll costs a
/// message to each node, and every node that polls reports the same totals: across nodes,
/// take the highest (<c>max</c>), not the sum. Every 30 seconds is plenty for a gauge that moves
/// as entities passivate.
/// </para>
/// <para>
/// A region that does not answer in time keeps its last figures until the next poll.
/// </para>
/// </remarks>
public sealed class ShardStatsPoller : ReceiveActor, IWithTimers
{
    private readonly TimeSpan _interval;

    public ShardStatsPoller(ActorShellMetrics metrics, TimeSpan interval)
    {
        _interval = interval;
        Receive<Poll>(_ => PollRegions());
        Receive<Polled>(polled => metrics.Sharded(
            polled.TypeName,
            polled.Stats.Regions.Values.Sum(region => region.Stats.Count),
            polled.Stats.Regions.Values.Sum(region => region.Stats.Values.Sum())));
        Receive<Status.Failure>(_ => { });
    }

    public ITimerScheduler Timers { get; set; } = null!;

    protected override void PreStart() =>
        Timers.StartPeriodicTimer(nameof(Poll), Poll.Instance, TimeSpan.Zero, _interval);

    private void PollRegions()
    {
        var sharding = ClusterSharding.Get(Context.System);
        var timeout = _interval / 2;
        foreach (var typeName in sharding.ShardTypeNames)
        {
            sharding.ShardRegion(typeName)
                .Ask<ClusterShardingStats>(new GetClusterShardingStats(timeout), _interval)
                .PipeTo(Self, success: stats => new Polled(typeName, stats));
        }
    }

    private sealed record Poll : INoSerializationVerificationNeeded
    {
        public static Poll Instance { get; } = new();
    }

    private sealed record Polled(string TypeName, ClusterShardingStats Stats)
        : INoSerializationVerificationNeeded;
}
