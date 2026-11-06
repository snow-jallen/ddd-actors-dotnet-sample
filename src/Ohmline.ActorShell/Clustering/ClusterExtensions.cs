using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Cluster.Hosting.SBR;
using Akka.Coordination.KubernetesApi;
using Akka.Discovery.KubernetesApi;
using Akka.Hosting;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Ohmline.ActorShell.Health;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.ActorShell.Clustering;

/// <summary>
/// How a node joins Ohmline's cluster: remoting, role, how it finds the others, downing, the
/// DistributedPubSub mediator that live updates and charger commands travel on, and the
/// serializer of the trace envelopes that any message may travel in. Every host
/// joins the same way: the charging nodes and, from section 16.7, the Device Gateway's.
/// </summary>
public static class ClusterExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Remoting on <c>Akka:Remote:Hostname</c> and <c>Akka:Remote:Port</c>, advertised as
        /// <c>Akka:Remote:PublicHostname</c> if set (a pod's IP), the node's role
        /// (<see cref="ClusterRoles"/>), its <see cref="ClusterFormation"/>, and the split-brain
        /// resolver written down rather than inherited. The app version is the release's
        /// <c>MAJOR.MINOR.PATCH</c> (<see cref="OhmlineAppVersion"/>), never Akka.NET's default,
        /// the entry assembly's version, which MinVer leaves at <c>MAJOR.0.0</c>.
        /// </summary>
        public AkkaConfigurationBuilder WithOhmlineCluster(
            IConfiguration configuration,
            IHostEnvironment environment,
            string role,
            string appVersion)
        {
            var formation = ClusterFormation.From(configuration, environment);
            var remote = new RemoteOptions
            {
                HostName = configuration["Akka:Remote:Hostname"] ?? "localhost",
                PublicHostName = configuration["Akka:Remote:PublicHostname"],
                Port = configuration.GetValue<int>("Akka:Remote:Port"),
            };

            akka
                .WithRemoting(remote)
                .WithClustering(new ClusterOptions
                {
                    Roles = [role],
                    AppVersion = appVersion,
                    SeedNodes = formation is StaticSeedNodes seeds ? [.. seeds.Addresses] : [],
                    SplitBrainResolver = DowningFor(formation),
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
                // Akka.NET 1.6 changes neither the wire nor rebalancing by itself (21.10).
                .WithUpgradePins()
                .AddHocon(
                    "akka.cluster.split-brain-resolver.stable-after = 20s",
                    HoconAddMode.Prepend);

            return formation switch
            {
                StaticSeedNodes => akka,
                SelfJoin => akka.AddStartup((system, _) =>
                {
                    var cluster = Cluster.Get(system);
                    cluster.Join(cluster.SelfAddress);
                }),
                KubernetesBootstrap bootstrap => akka.WithKubernetesBootstrap(
                    bootstrap, remote.PublicHostName ?? remote.HostName),
                null => throw new InvalidOperationException("No cluster formation was chosen."),
            };
        }

        /// <summary>
        /// Akka.Management's HTTP endpoint on every interface, advertised on the pod's address;
        /// contact points from the Kubernetes API, the pods labelled <c>akka-cluster</c> with the
        /// bootstrap's service name; the lease the split-brain resolver takes; and cluster
        /// bootstrap, which joins the cluster those pods already form, or forms it once enough
        /// of them agree that there is none (section 21.6).
        /// </summary>
        private AkkaConfigurationBuilder WithKubernetesBootstrap(
            KubernetesBootstrap bootstrap, string publicHostname) =>
            akka
                .WithAkkaManagement(
                    management =>
                    {
                        management.HostName = publicHostname;
                        management.Port = bootstrap.ManagementPort;
                        management.BindHostName = "0.0.0.0";
                        management.BindPort = bootstrap.ManagementPort;
                    },
                    autoStart: true)
                .WithKubernetesDiscovery(discovery =>
                    discovery.PodLabelSelector = "akka-cluster={0}")
                // The lease's own settings. LeaseMajorityOption only names the implementation;
                // without these the downing provider cannot create the lease, and the cluster
                // daemon fails to start with a NullReferenceException in LeaseProvider.GetLease.
                .WithKubernetesLease(new KubernetesLeaseOption())
                .WithClusterBootstrap(
                    options =>
                    {
                        options.ContactPointDiscovery.ServiceName = bootstrap.ServiceName;
                        options.ContactPointDiscovery.PortName = "management";
                        options.ContactPointDiscovery.RequiredContactPointsNr =
                            bootstrap.RequiredContactPoints;
                        options.ContactPointDiscovery.ContactWithAllContactPoints = true;
                        options.ContactPointDiscovery.StableMargin = TimeSpan.FromSeconds(5);
                    },
                    autoStart: true);
    }

    /// <summary>
    /// Keep-majority: Akka.NET's default since 1.5.2, written down. After 20 s without a change
    /// in reachability, the side with most nodes downs the other side. On Kubernetes,
    /// lease-majority: the side that holds a Kubernetes lease survives, so two halves of equal
    /// size, or a minority that cannot count the majority, never both stay up. The lease is a
    /// custom resource (<c>leases.akka.io</c>) installed once per cluster, never by the app.
    /// </summary>
    private static SplitBrainResolverOption DowningFor(ClusterFormation formation) =>
        formation is KubernetesBootstrap
            ? new LeaseMajorityOption { LeaseImplementation = new KubernetesLeaseOption() }
            : new KeepMajorityOption();
}
