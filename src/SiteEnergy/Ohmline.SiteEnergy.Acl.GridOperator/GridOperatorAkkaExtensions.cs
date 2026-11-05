using Akka.Cluster.Hosting;
using Akka.Hosting;
using Ohmline.ActorShell.Clustering;
using Ohmline.ActorShell.Sharding;

namespace Ohmline.SiteEnergy.Acl.GridOperator;

public static class GridOperatorAkkaExtensions
{
    /// <summary>Durable: the singleton's name in the cluster.</summary>
    public const string PollerName = "grid-signal-poller";

    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// The Northgate poller as a cluster singleton on the oldest <c>charging</c> node, made
        /// by the container: its feed, the site budgets' region, its settings and the clock.
        /// </summary>
        public AkkaConfigurationBuilder AddGridOperatorAcl() =>
            akka.WithSingleton<GridSignalPoller>(
                PollerName,
                (_, _, resolver) => resolver.Props<GridSignalPoller>(),
                new ClusterSingletonOptions
                {
                    Role = ClusterRoles.Charging,
                    TerminationMessage = Stop.Instance,
                },
                createProxyToo: false);
    }
}
