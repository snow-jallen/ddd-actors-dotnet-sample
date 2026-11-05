using Akka.Actor;
using Akka.Hosting;

namespace Ohmline.ActorShell.Telemetry;

/// <summary>The actor shell's metrics in an Akka.Hosting actor system (section 18.2).</summary>
public static class ShellTelemetryExtensions
{
    /// <summary>How often the shard regions are polled, unless a host says otherwise.</summary>
    public static readonly TimeSpan ShardStatsInterval = TimeSpan.FromSeconds(30);

    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// For every node: dead letters and Ohmline's actors' lifecycles, counted. It switches
        /// on Akka.NET's actor telemetry events, which cost a message on the event stream for
        /// every actor that starts or stops. Persistent actors' recoveries are timed by the shell
        /// itself.
        /// </summary>
        public AkkaConfigurationBuilder WithShellMetrics() =>
            akka
                .AddHocon("akka.actor.telemetry.enabled = on", HoconAddMode.Prepend)
                .WithActors((system, _) =>
                {
                    var metrics = ActorShellMetrics.For(system);
                    system.ActorOf(
                        Props.Create(() => new DeadLetterMetricsListener(metrics)),
                        "dead-letter-metrics");
                    system.ActorOf(
                        Props.Create(() => new LifecycleMetricsListener(metrics)),
                        "lifecycle-metrics");
                });

        /// <summary>
        /// For a node with shard regions: their shards and entities, polled on a timer.
        /// </summary>
        public AkkaConfigurationBuilder WithShardStats(TimeSpan? interval = null) =>
            akka.WithActors((system, _) =>
            {
                var metrics = ActorShellMetrics.For(system);
                var every = interval ?? ShardStatsInterval;
                system.ActorOf(
                    Props.Create(() => new ShardStatsPoller(metrics, every)), "shard-stats");
            });
    }
}
