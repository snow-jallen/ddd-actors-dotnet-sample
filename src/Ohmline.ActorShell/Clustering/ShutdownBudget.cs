using Akka.Hosting;

namespace Ohmline.ActorShell.Clustering;

/// <summary>
/// How long a cluster member may take to stop (section 21.7). Kubernetes sends SIGTERM and kills
/// the pod <see cref="TerminationGracePeriod"/> later; the .NET host gives its services
/// <see cref="HostShutdownTimeout"/> of that; and Akka.NET's coordinated shutdown, which runs
/// inside it, never waits longer than the sum of its phases' timeouts, set here to the same 30
/// seconds. Each phase gets what its work needs: the gateway drains its chargers' connections in
/// <c>service-unbind</c>, a node hands its shards off in <c>cluster-sharding-shutdown-region</c>.
/// </summary>
public static class ShutdownBudget
{
    /// <summary><c>terminationGracePeriodSeconds</c> of the node and gateway pods.</summary>
    public static readonly TimeSpan TerminationGracePeriod = TimeSpan.FromSeconds(35);

    /// <summary>The host's <c>HostOptions.ShutdownTimeout</c>: the grace period less 5 s.</summary>
    public static readonly TimeSpan HostShutdownTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The phase in which the gateway closes its chargers' connections.</summary>
    public static readonly TimeSpan ServiceUnbind = TimeSpan.FromSeconds(10);

    /// <summary>The phase in which a node hands its shards to the other nodes.</summary>
    public static readonly TimeSpan ShardHandOff = TimeSpan.FromSeconds(6);

    /// <summary>Every phase's timeout, in the order the phases run: 30 seconds in all.</summary>
    public const string Hocon = """
        akka.coordinated-shutdown.phases {
          before-service-unbind.timeout = 1s
          service-unbind.timeout = 10s
          service-requests-done.timeout = 2s
          service-stop.timeout = 1s
          before-cluster-shutdown.timeout = 1s
          cluster-sharding-shutdown-region.timeout = 6s
          cluster-leave.timeout = 2s
          cluster-exiting.timeout = 2s
          cluster-exiting-done.timeout = 1s
          cluster-shutdown.timeout = 1s
          before-actor-system-terminate.timeout = 1s
          actor-system-terminate.timeout = 2s
        }
        """;

    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>The phases' timeouts, ahead of Akka.NET's defaults (75 s in all).</summary>
        public AkkaConfigurationBuilder WithShutdownBudget() =>
            akka.AddHocon(Hocon, HoconAddMode.Prepend);
    }
}
