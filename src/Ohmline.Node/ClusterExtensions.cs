using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Cluster.Hosting.SBR;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Ohmline.ActorShell.Clustering;

namespace Ohmline.Node;

/// <summary>
/// How a node joins Ohmline's cluster: remoting, role, seed nodes, downing, and the
/// DistributedPubSub mediator the live updates travel on.
/// </summary>
public static class ClusterExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Remoting on <c>Akka:Remote:Hostname</c> and <c>Akka:Remote:Port</c>, the
        /// <c>charging</c> role, the seed nodes in <c>Akka:Cluster:SeedNodes</c>, and the
        /// split-brain resolver written down rather than inherited. A development node without
        /// seed nodes joins itself and is a cluster of one; any other node without them is a
        /// configuration mistake and does not start.
        /// </summary>
        public AkkaConfigurationBuilder WithOhmlineCluster(
            IConfiguration configuration, IHostEnvironment environment)
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
                    Roles = [ClusterRoles.Charging],
                    SeedNodes = seedNodes,
                    // Akka.NET's defaults since 1.5.2, made explicit: after 20 s without a
                    // change in reachability, the side with most nodes downs the other side.
                    SplitBrainResolver = new KeepMajorityOption(),
                })
                // /health is Healthy only once this node is an Up member of the cluster.
                .WithAkkaClusterReadinessCheck()
                // Live updates from projections reach every charging node (section 14.4).
                .WithDistributedPubSub(ClusterRoles.Charging)
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
