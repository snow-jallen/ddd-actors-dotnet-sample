using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Cluster.Hosting.SBR;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Ohmline.ActorShell.Health;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.ActorShell.Clustering;

/// <summary>
/// How a node joins Ohmline's cluster: remoting, role, seed nodes, downing, the
/// DistributedPubSub mediator that live updates and charger commands travel on, and the
/// serializer of the trace envelopes that any message may travel in. Every host
/// joins the same way: the charging nodes and, from section 16.7, the Device Gateway's.
/// </summary>
public static class ClusterExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Remoting on <c>Akka:Remote:Hostname</c> and <c>Akka:Remote:Port</c>, the node's role
        /// (<see cref="ClusterRoles"/>), the seed nodes in <c>Akka:Cluster:SeedNodes</c>, and the
        /// split-brain resolver written down rather than inherited. A development node without
        /// seed nodes joins itself and is a cluster of one; any other node without them is a
        /// configuration mistake and does not start.
        /// </summary>
        public AkkaConfigurationBuilder WithOhmlineCluster(
            IConfiguration configuration, IHostEnvironment environment, string role)
        {
            var hostname = configuration["Akka:Remote:Hostname"] ?? "localhost";
            var port = configuration.GetValue<int>("Akka:Remote:Port");
            var seedNodes =
                configuration.GetSection("Akka:Cluster:SeedNodes").Get<string[]>() ?? [];

            if (seedNodes is [] && !environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Akka:Cluster:SeedNodes is empty. Only a development node may form a " +
                    "cluster on its own.");
            }

            akka
                .WithRemoting(hostname, port)
                .WithClustering(new ClusterOptions
                {
                    Roles = [role],
                    SeedNodes = seedNodes,
                    // Akka.NET's defaults since 1.5.2, made explicit: after 20 s without a
                    // change in reachability, the side with most nodes downs the other side.
                    SplitBrainResolver = new KeepMajorityOption(),
                })
                // Live while the actor system runs; ready only once this node is an Up member
                // of the cluster. The tags are written down: both checks come with defaults of
                // their own (section 18.5).
                .WithActorSystemLivenessCheck(tags: [HealthTags.Live, "akka"])
                .WithAkkaClusterReadinessCheck(tags: [HealthTags.Ready, "akka", "akka.cluster"])
                // On every node, whatever its role: live updates reach every charging node
                // (section 14.4), and a charger command reaches the gateway node that holds the
                // charger's connection (section 16.7).
                .WithDistributedPubSub(role: "")
                // A traced message may be sent to any node, from any node (section 17.4).
                .WithTraceEnvelopes()
                .AddHocon(
                    "akka.cluster.split-brain-resolver.stable-after = 20s",
                    HoconAddMode.Prepend);

            return seedNodes is []
                ? akka.AddStartup((system, _) =>
                {
                    var cluster = Cluster.Get(system);
                    cluster.Join(cluster.SelfAddress);
                })
                : akka;
        }
    }
}
