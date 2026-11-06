using Akka.Actor;
using Akka.Cluster;
using Ohmline.ActorShell.Clustering;

namespace Ohmline.Node.Tests;

/// <summary>The node's cluster setup (section 12.1), as the test node starts it.</summary>
public sealed class NodeClusterTests(ITestOutputHelper output) : ShardedNode(output)
{
    [Fact]
    public void Given_a_development_node_without_seeds_then_it_is_a_cluster_of_one()
    {
        var self = Cluster.Get(Sys).SelfMember;

        self.Status.ShouldBe(MemberStatus.Up);
        self.Roles.ShouldBe(["charging"]);
        Cluster.Get(Sys).State.Members.ShouldHaveSingleItem();
    }

    [Fact]
    public void Given_the_node_then_its_split_brain_resolver_keeps_the_majority_after_20_s()
    {
        var config = Sys.Settings.Config;

        config.GetString("akka.cluster.downing-provider-class")
            .ShouldStartWith("Akka.Cluster.SBR.SplitBrainResolverProvider");
        config.GetString("akka.cluster.split-brain-resolver.active-strategy")
            .ShouldBe("keep-majority");
        config.GetTimeSpan("akka.cluster.split-brain-resolver.stable-after")
            .ShouldBe(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Given_the_node_then_the_cluster_sees_its_release_as_its_app_version()
    {
        var release = OhmlineAppVersion.Of(typeof(Program).Assembly);

        Cluster.Get(Sys).SelfMember.AppVersion.ToString().ShouldBe(release);
    }

    [Fact]
    public void Given_the_node_then_its_shutdown_tasks_fit_in_the_host_s_shutdown_timeout()
    {
        var shutdown = CoordinatedShutdown.Get(Sys);

        shutdown.TotalTimeout.ShouldBeLessThanOrEqualTo(ShutdownBudget.HostShutdownTimeout);
        shutdown.Timeout(CoordinatedShutdown.PhaseClusterShardingShutdownRegion)
            .ShouldBe(TimeSpan.FromSeconds(6));
    }
}
